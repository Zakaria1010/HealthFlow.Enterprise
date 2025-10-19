using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using HealthFlow.Shared.Messaging;

namespace HealthFlow.Infrastructure.Messaging;

public static class RabbitMQServiceExtensions
{
    public static IServiceCollection AddRabbitMQ(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest",
                DispatchConsumersAsync = true
            };

            return factory.CreateConnection();
        });

        return services;
    }

    public static IServiceCollection AddRabbitMQPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessagePublisher>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<RabbitMQPublisher>>();
            return new RabbitMQPublisher(configuration, logger);
        });

        return services;
    }

    public static IServiceCollection AddRabbitMQWithHealthCheck(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMQPublisher(configuration);
        
        services.AddHealthChecks()
            .AddRabbitMQ(
                rabbitConnectionString: 
                    $"amqp://{configuration["RabbitMQ:UserName"]}:{configuration["RabbitMQ:Password"]}@" +
                    $"{configuration["RabbitMQ:HostName"]}:{configuration["RabbitMQ:Port"] ?? "5672"}/",
                name: "rabbitmq",
                tags: new[] { "messaging", "rabbitmq" });

        return services;
    }
}
