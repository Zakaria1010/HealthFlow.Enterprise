using Microsoft.Azure.Cosmos;
namespace HealthFlow.Infrastructure.Database;

public static class CosmosDBServiceExtensions {

    public static IServiceCollection AddCosmosDB(this IServiceCollection services, IConfiguration configuration)
    {   
        // Read configuration from appsettings.json or environment
        var cosmosEndpoint = configuration["CosmosDb:AccountEndpoint"];
        var cosmosKey = configuration["CosmosDb:AccountKey"];
        var cosmosDatabase = configuration["CosmosDb:DatabaseId"];
        var cosmosContainer = configuration["CosmosDb:ContainerId"];

        if (string.IsNullOrEmpty(cosmosEndpoint) || string.IsNullOrEmpty(cosmosKey))
            throw new InvalidOperationException("CosmosDB configuration is missing.");

        // Create and register the CosmosClient as a Singleton
        services.AddSingleton(sp =>
        {
            var clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway, // Use Direct for better perf
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };

            return new CosmosClient(cosmosEndpoint, cosmosKey, clientOptions);
        });

        return services;
    }
}

public class CosmosDbSettings
{
    public string DatabaseName { get; }
    public CosmosDbSettings(string databaseName) => DatabaseName = databaseName;
}