using AttendanceService.Background;
using AttendanceService.Database;
using AttendanceService.HealthCheck;
using Confluent.Kafka;
using Confluent.Kafka.Options;
using HealthChecks.ApplicationStatus.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

string postgres_server = builder.Configuration["POSTGRES_SERVER"] ?? "";
string postgres_database = builder.Configuration["POSTGRES_DATABASE"] ?? "";
string postgres_username = builder.Configuration["POSTGRES_USERNAME"] ?? "";
string postgres_password = builder.Configuration["POSTGRES_PASSWORD"] ?? "";

string postgres_con_string = $"Host={postgres_server};Username={postgres_username};Password={postgres_password};Database={postgres_database}";

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddKafkaClient()
    .Configure(options => {
        options.Configure(new ConsumerConfig {
            BootstrapServers = configuration["KAFKA_URL"]
        }); 
});

builder.Services.AddNpgsqlDataSource(postgres_con_string);
builder.Services.AddDbContext<AttendanceDbContext>(options => {
    options.UseNpgsql(postgres_con_string);
});

// Background updates
builder.Services.AddScoped<IDataUpdater, KafkaUpdater>();
builder.Services.AddSingleton<GraphQLClientFactory>();
builder.Services.AddHostedService<DataFetchService>();
builder.Services.AddHostedService<DataUpdaterBackgroundService>();

builder.Services.AddHealthChecks()
    .AddNpgSql("postgres", tags: new [] { "ready" })
    .AddApplicationStatus("appstatus", tags: new [] { "live" })
    .AddCheck<DatabaseCreationHealthCheck>("database_creation", tags: new [] { "startup" });

builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options => {
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = "openapi";
    options.DocumentTitle = "OpenAPI documentation";
});

app.UseHttpsRedirection();

app.MapHealthChecks("/health/startup", new HealthCheckOptions {
    Predicate = healthcheck => healthcheck.Tags.Contains("startup") 
});
app.MapHealthChecks("/health/live", new HealthCheckOptions {
    Predicate = healthcheck => healthcheck.Tags.Contains("live") 
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions {
    Predicate = healthcheck => healthcheck.Tags.Contains("ready") 
});

app.MapControllers();

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

app.Run();
