using HealthFlow.Services.Analytics.Data;
using HealthFlow.Shared.Data;
using HealthFlow.Shared.Models;
using Microsoft.Azure.Cosmos;

namespace HealthFlow.Services.Analytics.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCosmosDb(this IServiceCollection services, IConfiguration configuration)
        {
            var cosmosConfig = configuration.GetSection("CosmosDb");
            var databaseId = cosmosConfig["DatabaseId"] ?? "HealthFlowAnalytics";
            var containerId = cosmosConfig["ContainerId"] ?? "Events";

            services.AddSingleton(provider =>
            {
                var endpoint = cosmosConfig["AccountEndpoint"];
                var key = cosmosConfig["AccountKey"];

                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
                {
                    throw new InvalidOperationException("Cosmos DB configuration is missing. Please check your app settings.");
                }

                var client = new CosmosClient(endpoint, key, new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                });

                return client;
            });

            // Register the generic repository
            services.AddScoped<IRepository<AnalyticsEvent>>(provider =>
            {
                var cosmosClient = provider.GetRequiredService<CosmosClient>();
                var logger = provider.GetRequiredService<ILogger<CosmosRepository<AnalyticsEvent>>>();
                return new CosmosRepository<AnalyticsEvent>(cosmosClient, databaseId, containerId, logger);
            });

            return services;
        }

        public static async Task InitializeCosmosDb(this IServiceProvider serviceProvider)
        {
            try
            {
                var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
                var databaseId = serviceProvider.GetRequiredService<IConfiguration>()["CosmosDb:DatabaseId"] ?? "HealthFlowAnalytics";
                var containerId = serviceProvider.GetRequiredService<IConfiguration>()["CosmosDb:ContainerId"] ?? "Events";

                // Create database if not exists
                var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
                var database = databaseResponse.Database;

                // Create container if not exists
                await database.CreateContainerIfNotExistsAsync(new ContainerProperties
                {
                    Id = containerId,
                    PartitionKeyPath = "/partitionKey"
                });

                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Cosmos DB database {DatabaseId} and container {ContainerId} initialized successfully", 
                    databaseId, containerId);
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Error initializing Cosmos DB");
                throw;
            }
        }
    }
}