using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthFlow.Shared.Messaging
{
    public static class ServiceCollectionExtensions
    {
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
}