using HealthFlow.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using HealthFlow.Shared.Messaging;


namespace HealthFlow.Shared.Messaging
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQPublisher> _logger;
        private bool _disposed = false;

        public bool IsConnected => _connection?.IsOpen == true && !_disposed;

        public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
        {
            _logger = logger;
            
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                    UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = configuration["RabbitMQ:Password"] ?? "guest",
                    Port = AmqpTcpEndpoint.UseDefaultPort,
                    VirtualHost = "/",
                    DispatchConsumersAsync = true,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                // Ensure exchanges exist
                _channel.ExchangeDeclare("patient.events", ExchangeType.Topic, durable: true);
                _channel.ExchangeDeclare("analytics.events", ExchangeType.Topic, durable: true);
                _channel.ExchangeDeclare("system.events", ExchangeType.Fanout, durable: true);
                
                _logger.LogInformation("RabbitMQ publisher connected to {HostName}", factory.HostName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public async Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken) where T : class
        {
            await PublishAsync("patient.events", queueName, message);
        }

        public async Task PublishAsync(string queueName, PatientMessage message, CancellationToken cancellationToken)
        {
            await PublishAsync("patient.events", queueName, message);
        }

        public async Task PublishAsync(string exchange, string routingKey, PatientMessage message)
        {
            await PublishAsync<PatientMessage>(exchange, routingKey, message);
        }

        public async Task PublishAsync<T>(string exchange, string routingKey, T message) where T : class
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("RabbitMQ connection is not available");
            }

            try
            {
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
                
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.ContentType = "application/json";

                _channel.BasicPublish(
                    exchange: exchange,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);
                
                _logger.LogDebug("Message published to {Exchange}/{RoutingKey}: {MessageType}", 
                    exchange, routingKey, typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to {Exchange}/{RoutingKey}", exchange, routingKey);
                throw;
            }
        }

        public IConnection GetConnection()
        {
            return _connection;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _channel?.Close();
                _channel?.Dispose();
                _connection?.Close();
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}