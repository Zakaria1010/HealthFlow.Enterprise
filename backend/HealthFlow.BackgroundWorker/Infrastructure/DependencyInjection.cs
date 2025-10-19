using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HealthFlow.Infrastructure.Messaging;
using HealthFlow.Infrastructure.Database;

namespace HealthFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add EF Core / Cosmos Server
        services.AddCosmosDB(configuration);

        // Add RabbitMQ
        Services.AddRabbitMQWithHealthCheck(builder.Configuration);

        return services;
    }
}
