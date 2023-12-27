using AttendanceService.Database;
using HealthChecks.ApplicationStatus.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

string postgres_server = builder.Configuration["POSTGRES_SERVER"] ?? "";
string postgres_database = builder.Configuration["POSTGRES_DATABASE"] ?? "";
string postgres_username = builder.Configuration["POSTGRES_USERNAME"] ?? "";
string postgres_password = builder.Configuration["POSTGRES_PASSWORD"] ?? "";

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddNpgsqlDataSource($"Host={postgres_server};Username={postgres_username};Password={postgres_password};Database={postgres_database}");
builder.Services.AddDbContext<AttendanceDbContext>(options => {
    options.UseNpgsql();
});
builder.Services.AddHealthChecks()
    .AddNpgSql("postgres", tags: new [] { "ready" })
    .AddApplicationStatus("appstatus", tags: new [] { "live" });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health/live", new HealthCheckOptions {
    Predicate = healthcheck => healthcheck.Tags.Contains("live") 
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions {
    Predicate = healthcheck => healthcheck.Tags.Contains("ready") 
});

app.Run();
