using HealthFlow.Shared.Models;
using RabbitMQ.Client;

namespace HealthFlow.Shared.Messaging;
public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName , T message, CancellationToken cancellationToken) where T : class;
    Task PublishAsync(string queueName, PatientMessage message, CancellationToken cancellationToken);
    IConnection GetConnection();
    bool IsConnected { get; }
}