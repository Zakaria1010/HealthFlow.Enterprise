using HealthFlow.BackgroundWorker;
using HealthFlow.BackgroundWorker.Services;
using HealthFlow.BackgroundWorker.Consumers;
using HealthFlow.BackgroundWorker.Data;
using HealthFlow.Infrastructure;
using HealthFlow.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Cosmos;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // 🔌 Add Infrastructure dependencies (RabbitMQ + Cosmos)
        services.AddInfrastructure(configuration);

        // 🧩 Register background processing
        services.AddSingleton<PatientProcessingChannel>();
        services.AddSingleton<IProcessingRepository, ProcessingRepository>();
        services.AddHostedService<PatientProcessingService>();
        services.AddHostedService<PatientEventConsumer>();    
    })
    .Build();

// Initialize database on startup
await InitializeDatabaseAsync(host.Services);

await host.RunAsync();

async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
    var options = scope.ServiceProvider.GetRequiredService<IOptions<CosmosDbOptions>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>(); // Get configuration from DI

    try
    {
        // Create database if not exists
        var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(options.Value.DatabaseId);
        var database = databaseResponse.Database;
        var partitionKeyPath = configuration["CosmosDb:PartitionKeyPath"];
        // Create container if not exists
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = options.Value.ContainerId,
            PartitionKeyPath = "/partitionKey"
        });

        logger.LogInformation("Background Worker database initialized: {DatabaseId}", options.Value.DatabaseId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing Background Worker database");
        throw;
    }
}