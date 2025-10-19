using HealthFlow.Services.Analytics.Extensions;
using HealthFlow.Services.Analytics.Hubs;
using HealthFlow.Services.Analytics.HealthChecks;
using HealthFlow.Services.Analytics.Consumers; // Add this
using HealthFlow.Shared.Messaging;
using HealthFlow.Shared.Data;
using HealthFlow.Services.Analytics.Data;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cosmos DB
builder.Services.AddCosmosDb(builder.Configuration);

// RabbitMQ from Shared project
builder.Services.AddRabbitMQWithHealthCheck(builder.Configuration);

// Add Analytics Event Consumer
builder.Services.AddHostedService<AnalyticsEventConsumer>();

// Analytics Repository
// 2️⃣ Register AnalyticsRepository (Scoped or Singleton, depending on usage)
builder.Services.AddScoped<IAnalyticsRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<AnalyticsRepository>>();

    var dbId = config["CosmosDb:DatabaseId"];
    var containerId = config["CosmosDb:ContainerId"];

    return new AnalyticsRepository(cosmosClient, dbId, containerId, logger);
});

// SignalR for real-time dashboard updates
builder.Services.AddSignalR();

// CORS for web frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // important!
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<CosmosDbHealthCheck>("analytics");


var app = builder.Build();

// Initialize Cosmos DB
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.InitializeCosmosDb();
}

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AnalyticsHub>("/analyticsHub");
app.MapHealthChecks("/health");

app.Run();