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

        // Infrastructure
        services.AddInfrastructure(configuration);

        // Background processing
        services.AddSingleton<PatientProcessingChannel>();
        services.AddSingleton<IProcessingRepository, ProcessingRepository>();
        
        // Register consumers - make sure these are added!
        services.AddHostedService<PatientProcessingService>();
        services.AddHostedService<PatientEventConsumer>(); // This must be present!
    })
    .Build();

// Initialize database on startup
await InitializeDatabaseAsync(host.Services);

await host.RunAsync();

static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();

    var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
    var options = scope.ServiceProvider.GetRequiredService<IOptions<CosmosDbOptions>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var databaseId = options.Value.DatabaseId;
    var containerId = options.Value.ContainerId;
    var partitionKeyPath = configuration["CosmosDb:PartitionKeyPath"];

    try
    {
        logger.LogInformation("Initializing Cosmos DB: {DatabaseId}/{ContainerId}", databaseId, containerId);

        // Validate endpoint reachability before attempting creation
        var accountEndpoint = configuration["CosmosDb:AccountEndpoint"];
        if (!Uri.TryCreate(accountEndpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new InvalidOperationException($"Invalid CosmosDb AccountEndpoint: '{accountEndpoint}'.");
        }

        logger.LogInformation("Connecting to Cosmos DB at {Endpoint}", endpointUri);

        // Create database if not exists
        var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
        var database = databaseResponse.Database;

        // Create container if not exists
        var containerProperties = new ContainerProperties
        {
            Id = containerId,
            PartitionKeyPath = partitionKeyPath
        };

        var containerResponse = await database.CreateContainerIfNotExistsAsync(containerProperties);

        logger.LogInformation(
            "Cosmos DB initialized successfully. Database: {DatabaseId}, Container: {ContainerId}, PartitionKey: {PartitionKeyPath}",
            databaseId, containerId, partitionKeyPath);
    }
    catch (CosmosException cex)
    {
        logger.LogError(cex, "Cosmos DB returned an error during initialization. Status: {StatusCode}, Message: {Message}",
            cex.StatusCode, cex.Message);
        throw;
    }
    catch (HttpRequestException hex)
    {
        logger.LogError(hex, "Network error connecting to Cosmos DB at {Endpoint}. Check your endpoint, DNS, and firewall settings.",
            configuration["CosmosDb:AccountEndpoint"]);
        throw;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error initializing Cosmos DB database or container.");
        throw;
    }
}
