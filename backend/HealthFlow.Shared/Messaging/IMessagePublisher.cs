using HealthFlow.Shared.Models;
using RabbitMQ.Client;

namespace HealthFlow.Shared.Messaging;
public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName , T message) where T : class;
    Task PublishAsync(string queueName, PatientMessage message);
    IConnection GetConnection();
}