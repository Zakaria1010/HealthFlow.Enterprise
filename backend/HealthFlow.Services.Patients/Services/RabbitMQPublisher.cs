using RabbitMQ.Client;
using HealthFlow.Services.Patients.Models;
using HealthFlow.Shared.Messaging;
using HealthFlow.Shared.Models;
using System.Text.Json;
using System;
using System.Text;

namespace HealthFlow.Services.Patients.Services;
public class RabbitMQPublisher: IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQPublisher> _logger;

    public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
    {
        _logger = logger;
        
        var factory = new ConnectionFactory()
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            Port = AmqpTcpEndpoint.UseDefaultPort,  // ✅ fixed here
            VirtualHost = "/",
            DispatchConsumersAsync = true           // ✅ async consumer handling
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Ensure exchange exists
        _channel.ExchangeDeclare(
            exchange: "patient.events",
            type: "topic",   // ✅ string literal
            durable: true
        );
        
        _logger.LogInformation("RabbitMQ publisher connectec to {HostName}", factory.HostName);
    }

    public async Task PublishAsync<T>(string queueName, T message) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var body = Encoding.UTF8.GetBytes(json);

            _channel.BasicPublish(
                exchange: "patient.events",
                routingKey: queueName,
                basicProperties: null,
                body: body);

            _logger.LogInformation("Message published to {QueueName}: {MessageType}", queueName, typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to {QueueName}", queueName);
            throw;
        }
    }

    public async Task PublishAsync(string queueName, PatientMessage message)
    {
        await PublishAsync(queueName, (object)message);
    }

    public IConnection GetConnection()
    {
        return _connection;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}