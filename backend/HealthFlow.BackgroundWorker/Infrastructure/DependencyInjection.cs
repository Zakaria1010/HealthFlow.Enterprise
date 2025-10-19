using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HealthFlow.Infrastructure.Messaging;
using HealthFlow.Infrastructure.Database;
using Microsoft.Azure.Cosmos;
using HealthFlow.BackgroundWorker.Data;

namespace HealthFlow.Infrastructure;


public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // RabbitMQ from Shared project
        services.AddRabbitMQWithHealthCheck(configuration);

        // Background Worker's own Cosmos DB configuration
        services.Configure<CosmosDbOptions>(options =>
        {
            options.DatabaseId = configuration["CosmosDb:DatabaseId"] ?? "HealthFlowProcessing";
            options.ContainerId = configuration["CosmosDb:ContainerId"] ?? "ProcessedEvents";
        });

        // Cosmos Client for Background Worker's own database
        services.AddSingleton(provider =>
        {
            var endpoint = configuration["CosmosDb:AccountEndpoint"];
            var key = configuration["CosmosDb:AccountKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Cosmos DB configuration is missing.");
            }

            return new CosmosClient(endpoint, key, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        });

        return services;
    }
}



