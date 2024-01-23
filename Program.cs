using AttendanceService.Background;
using AttendanceService.Common;
using AttendanceService.Concerts;
using AttendanceService.Database;
using AttendanceService.GraphQL;
using AttendanceService.Kafka;
using AttendanceService.Members;
using AttendanceService.Rehearsals;
using Confluent.Kafka;
using HealthChecks.ApplicationStatus.DependencyInjection;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Events;

public class Program
{
    private const string DbConnectionStringName = "Database";

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
        ConfigureApplication(builder);
        ConfigureHttpClients(builder);
        ConfigureGraphQLClients(builder);
        ConfigureLogging(builder);
        ConfigureKafka(builder);
        ConfigureOpenApi(builder);
        ConfigureBackgroundServices(builder);
        ConfigureDatabase(builder);
        ConfigureMetrics(builder);
        ConfigureHealthChecks(builder);
    }

    private static void ConfigureApplication(WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();
    }

    private static void ConfigureHttpClients(WebApplicationBuilder builder) {
        builder.Services.AddHeaderPropagation(options => {
            options.Headers.Add("X-Correlation-Id");
        });

        IAsyncPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        IAsyncPolicy<HttpResponseMessage> circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        builder.Services.AddHttpClient("graphql_members", client => {
            client.BaseAddress = new Uri(builder.Configuration["MembersService:GraphQL:Url"]
                ?? throw new Exception("MembersService:GraphQL:Url config value is missing"));
        }).AddPolicyHandler(retryPolicy)
          .AddPolicyHandler(circuitBreakerPolicy);

        builder.Services.AddHttpClient("graphql_planning", client => {
            client.BaseAddress = new Uri(builder.Configuration["PlanningService:GraphQL:Url"]
                ?? throw new Exception("PlanningService:GraphQL:Url config value is missing"));
        }).AddPolicyHandler(retryPolicy)
          .AddPolicyHandler(circuitBreakerPolicy);
    }

    private static void ConfigureGraphQLClients(WebApplicationBuilder builder) {
        builder.Services.AddSingleton<GraphQLClientFactory>();
        builder.Services.AddGraphQLClient<IDataFetchService<Member>, MemberGraphQLService>("graphql_members");
        builder.Services.AddGraphQLClient<IDataFetchService<Concert>, ConcertGraphQLService>("graphql_planning");
        builder.Services.AddGraphQLClient<IDataFetchService<Rehearsal>, RehearsalGraphQLService>("graphql_planning");
    }

    private static void ConfigureLogging(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, config) => {
            config.ReadFrom.Configuration(builder.Configuration)
                .Enrich.WithCorrelationIdHeader("X-Correlation-Id");
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
        builder.Services.AddScoped<IDataUpdater, KafkaUpdater>();
        builder.Services.AddHostedService<DataUpdaterBackgroundService>();
        builder.Services.AddHostedService<DataFetchService>();
    }

    private static void ConfigureKafka(WebApplicationBuilder builder)
    {
        string? kafkaUrl = builder.Configuration["KAFKA_URL"];
        builder.Services.AddKafkaClient()
            .Configure(options => {
                options.Configure(new ConsumerConfig {
                    BootstrapServers = kafkaUrl
                }).Serialize(new JsonMessageSerializer<KafkaMessage>())
                  .Deserialize(new JsonMessageSerializer<KafkaMessage>());
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

    private static void ConfigureHealthChecks(WebApplicationBuilder builder) {
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AttendanceDbContext>(
                    tags: new [] {"ready"},
                    failureStatus: HealthStatus.Unhealthy)
            .AddApplicationStatus(
                    tags: new [] {"live"}, 
                    failureStatus: HealthStatus.Unhealthy);

        string? urlsEnv = builder.Configuration["ASPNETCORE_URLS"];
        List<string> urls = urlsEnv?.Split(",").ToList() ?? new List<string>();
        builder.Services
            .AddHealthChecksUI(setup => {
                for (int i = 0; i < urls.Count; i++) {
                    setup.AddHealthCheckEndpoint($"live{i}", $"{urls[i]}/health/live");
                    setup.AddHealthCheckEndpoint($"ready{i}", $"{urls[i]}/health/ready");
                }
            })
            .AddInMemoryStorage();
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
        string app_base = app.Configuration["APP_BASE"] ?? "/";
        app.UsePathBase(app_base);
        app.UseRouting();

        ConfigureSwaggerUI(app);

        app.MapPrometheusScrapingEndpoint();
        app.UseHeaderPropagation();

        ConfigureWebApplication(app);
        ConfigureApplicationLogging(app);
        ConfigureApplicationHealthChecks(app);
    }

    private static void ConfigureSwaggerUI(WebApplication app)
    {
        string app_base = app.Configuration["APP_BASE"] ?? "";

        // Configure the HTTP request pipeline.
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"{app_base}/swagger/v1/swagger.json", "v1");
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

    private static void ConfigureApplicationHealthChecks(WebApplication app) {
        app.MapHealthChecks("/health/live", new HealthCheckOptions {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
        app.UseHealthChecksPrometheusExporter("/healthmetrics");
        app.MapHealthChecksUI();
    }
}
