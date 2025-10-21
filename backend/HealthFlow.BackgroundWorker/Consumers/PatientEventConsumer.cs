using HealthFlow.Shared.Messaging;
using HealthFlow.Shared.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using HealthFlow.BackgroundWorker.Services;

namespace HealthFlow.BackgroundWorker.Consumers
{
    public class PatientEventConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly PatientProcessingChannel _processingChannel;
        private readonly ILogger<PatientEventConsumer> _logger;
        private bool _disposed = false;

        public PatientEventConsumer(
            IMessagePublisher messagePublisher,
            PatientProcessingChannel processingChannel,
            ILogger<PatientEventConsumer> logger)
        {
            _processingChannel = processingChannel;
            _logger = logger;
            
            try
            {
                _logger.LogInformation("🔄 Initializing PatientEventConsumer with async mode...");
                
                _connection = messagePublisher.GetConnection();
                _channel = _connection.CreateModel();
                
                // Declare exchange and queue
                _channel.ExchangeDeclare("patient.events", "topic", durable: true);
                _channel.QueueDeclare("patient-processing", durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind("patient-processing", "patient.events", "patient.*");
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                
                _logger.LogInformation("✅ PatientEventConsumer initialized successfully for async mode");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize PatientEventConsumer");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 PatientEventConsumer starting in async mode...");

            // Use AsyncEventingBasicConsumer for async mode
            var consumer = new AsyncEventingBasicConsumer(_channel);
            
            consumer.Received += async (model, ea) =>
            {
                _logger.LogInformation("📨 Message received from RabbitMQ. DeliveryTag: {DeliveryTag}", ea.DeliveryTag);
                
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);
                    
                    _logger.LogDebug("Raw message JSON: {MessageJson}", messageJson);
                    
                    var message = JsonSerializer.Deserialize<PatientMessage>(
                        messageJson, 
                        new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                    
                    if (message != null)
                    {
                        _logger.LogInformation(
                            "✅ Successfully deserialized message: {EventType} for patient {PatientId}", 
                            message.EventType, message.PatientId);
                        
                        // Process the message asynchronously
                        await _processingChannel.WriteAsync(message, stoppingToken);
                        
                        // Acknowledge the message
                        _channel.BasicAck(ea.DeliveryTag, false);
                        _logger.LogInformation("✅ Message acknowledged and sent to processing channel");
                    }
                    else
                    {
                        _logger.LogWarning("❌ Failed to deserialize PatientMessage: {MessageBody}", messageJson);
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing RabbitMQ message. DeliveryTag: {DeliveryTag}", ea.DeliveryTag);
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            // Start consuming
            var consumerTag = _channel.BasicConsume("patient-processing", false, consumer);
            _logger.LogInformation("🎯 Async consumer started. ConsumerTag: {ConsumerTag}", consumerTag);

            // Keep the service running
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("🛑 PatientEventConsumer stopping due to cancellation");
            }
        }

        public override void Dispose()
        {
            if (!_disposed)
            {
                _channel?.Dispose();
                _connection?.Dispose();
                _disposed = true;
            }
            
            base.Dispose();
        }
    }
}