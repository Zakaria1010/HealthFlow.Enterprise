using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthFlow.Services.Patients.Data;
using HealthFlow.Services.Patients.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.FeatureManagement;
using HealthFlow.Shared.Messaging;
using HealthChecks.RabbitMQ;
using Polly;
using HealthFlow.Shared.Messaging; 


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework with SQL Server and retry policy
builder.Services.AddDbContext<PatientDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PatientsDatabase"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
});

// SignalR for real-time updates
builder.Services.AddSignalR();

// RabbitMQ
// RabbitMQ Publisher from Shared project
builder.Services.AddRabbitMQWithHealthCheck(builder.Configuration);

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PatientDbContext>();

// Feature Management
builder.Services.AddFeatureManagement();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize Database with retry policy
var retryPolicy = Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            Console.WriteLine($"Database initialization attempt {retryCount} failed. Waiting {timeSpan} before next retry. Error: {exception.Message}");
        });

await retryPolicy.ExecuteAsync(async () =>
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PatientDbContext>();
    await dbContext.Database.MigrateAsync();
    Console.WriteLine("Database migrated successfully");
});

app.UseCors("AllowAll");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PatientHub>("/patientHub");
app.MapHealthChecks("/health");
app.Run();
