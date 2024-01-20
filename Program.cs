using AttendanceService.Background;
using AttendanceService.Database;
using Confluent.Kafka;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Events;

public class Program
{
    private const string DbConnectionStringName = "AttendanceDatabase";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureBuilder(builder);

        var app = builder.Build();
        InitializeDatabase(app);
        ConfigureApplication(app);

        app.Run();
    }

    private static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();

        ConfigureHttpClients(builder);
        ConfigureLogging(builder);
        ConfigureKafka(builder);
        ConfigureOpenApi(builder);
        ConfigureBackgroundServices(builder);
        ConfigureDatabase(builder);
        ConfigureMetrics(builder);
    }

    private static void ConfigureHttpClients(WebApplicationBuilder builder) {
        builder.Services.AddHeaderPropagation(options => {
            options.Headers.Add("X-Correlation-Id");
        });

        string? graphql_members_base = builder.Configuration["MembersService:GraphQL:Url"];
        string? graphql_planning_base = builder.Configuration["PlanningService:GraphQL:Url"];

        builder.Services.AddHttpClient("graphql_members", client => {
            client.BaseAddress = new Uri(graphql_members_base
                ?? throw new Exception("MembersService:GraphQL:Url config value is missing"));
        }).AddHeaderPropagation();

        builder.Services.AddHttpClient("graphql_concerts", client => {
            client.BaseAddress = new Uri(graphql_planning_base
                ?? throw new Exception("PlanningService:GraphQL:Url config value is missing"));
        }).AddHeaderPropagation();
        
        builder.Services.AddHttpClient("graphql_rehearsals", client => {
            client.BaseAddress = new Uri(graphql_planning_base
                ?? throw new Exception("PlanningService:GraphQL:Url config value is missing"));
        }).AddHeaderPropagation();
    }

    private static void ConfigureLogging(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, config) => {
            config.ReadFrom.Configuration(builder.Configuration)
                .Enrich.WithCorrelationIdHeader("X-Correlation-Id")
                .CreateLogger();
        });
    }

    private static void ConfigureOpenApi(WebApplicationBuilder builder)
    {
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder)
    {
        string? connectionString =
            builder.Configuration.GetConnectionString(DbConnectionStringName);

        builder.Services.AddDbContext<AttendanceDbContext>(options => {
            if (builder.Environment.IsDevelopment()) {
                options.UseSqlite(connectionString);
            }
            else {
                options.UseNpgsql(connectionString);
            }
        });
    }

    private static void ConfigureBackgroundServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IDataUpdater, KafkaGraphQLUpdater>();
        builder.Services.AddScoped<IGraphQLClientFactory, GraphQLClientFactory>();
        builder.Services.AddHostedService<DataUpdaterBackgroundService>();
        builder.Services.AddHostedService<DataFetchService>();
    }

    private static void ConfigureKafka(WebApplicationBuilder builder)
    {
        string? kafkaUrl = builder.Configuration["KAFKA_URL"];
        builder.Services.AddKafkaClient()
            .Configure(options => {
                options.Configure(new ProducerConfig {
                    BootstrapServers = kafkaUrl
                });
            });
    }

    private static void ConfigureMetrics(WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .WithMetrics(builder => {
                builder.AddPrometheusExporter();

                builder.AddMeter(
                    "Microsoft.AspNetCore.Hosting",
                    "Microsoft.AspNetCore.Server.Kestrel");
            });
    }

    private static void InitializeDatabase(WebApplication app)
    {
        IServiceScopeFactory scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
        using IServiceScope scope = scopeFactory.CreateScope();
        DbContext dbContext = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database set to connection string {0}",
                              app.Configuration.GetConnectionString(DbConnectionStringName));
        logger.LogInformation("Ensuring database is created.");
        bool wasCreated = dbContext.Database.EnsureCreated();
        if (wasCreated) {
            logger.LogInformation("Database was created.");
        }
    }

    private static void ConfigureApplication(WebApplication app)
    {
        ConfigureSwaggerUI(app);

        app.MapPrometheusScrapingEndpoint();
        app.UseHeaderPropagation();

        ConfigureWebApplication(app);
        ConfigureApplicationLogging(app);
    }

    private static void ConfigureSwaggerUI(WebApplication app)
    {
        // Configure the HTTP request pipeline.
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            options.RoutePrefix = "openapi";
            options.DocumentTitle = "OpenAPI documentation";
        });
    }

    private static void ConfigureWebApplication(WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.UseExceptionHandler(a => a.Run(async context =>
        {
            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;
            if (exception is null) {
                context.RequestServices
                    .GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>()
                    .LogError(exception, "An error has occurred while processing request");
            }
           
            await context.Response.WriteAsJsonAsync(new { error = "An error has occurred while processing request" });
        }));
        app.MapControllers();
    }

    private static void ConfigureApplicationLogging(WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            // Customize the message template
            options.MessageTemplate = "Handled {RequestPath}";

            // Emit debug-level events instead of the defaults
            options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Debug;

            // Attach additional properties to the request completion event
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            };
        });
    }
}
