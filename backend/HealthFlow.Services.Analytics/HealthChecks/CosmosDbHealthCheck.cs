using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthFlow.Services.Analytics.HealthChecks
{
    public class CosmosDbHealthCheck : IHealthCheck
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName;

        public CosmosDbHealthCheck(CosmosClient cosmosClient, string databaseName)
        {
            _cosmosClient = cosmosClient;
            _databaseName = databaseName;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var database = _cosmosClient.GetDatabase(_databaseName);
                await database.ReadAsync(cancellationToken: cancellationToken);
                
                return HealthCheckResult.Healthy("Cosmos DB is healthy");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Cosmos DB is unhealthy", ex);
            }
        }
    }

    public static class CosmosDbHealthCheckExtensions
    {
        public static IHealthChecksBuilder AddCosmosDb(
            this IHealthChecksBuilder builder,
            string connectionString,
            string databaseName,
            string name = "cosmosdb",
            HealthStatus failureStatus = HealthStatus.Unhealthy,
            IEnumerable<string> tags = null)
        {
            return builder.Add(new HealthCheckRegistration(
                name,
                provider =>
                {
                    var cosmosClient = new CosmosClient(connectionString);
                    return new CosmosDbHealthCheck(cosmosClient, databaseName);
                },
                failureStatus,
                tags));
        }
    }
}