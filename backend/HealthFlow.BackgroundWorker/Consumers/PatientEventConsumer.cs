using HealthFlow.Shared.Messaging;
using HealthFlow.Shared.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using HealthFlow.BackgroundWorker.Services;

namespace HealthFlow.BackgroundWorker.Consumers;
public class PatientEventConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly PatientProcessingChannel _processingChannel;
    private readonly ILogger<PatientEventConsumer> _logger;

    public PatientEventConsumer(
        IMessagePublisher messagePublisher,
        PatientProcessingChannel processingChannel,
        ILogger<PatientEventConsumer> logger)
    {
        _processingChannel = processingChannel;
        _logger = logger;

        _connection = messagePublisher.GetConnection();
        _channel = _connection.CreateModel();

        // Declare RabbitMQ exchange and queue
        _channel.ExchangeDeclare("patient.events", "topic", durable: true);
        _channel.QueueDeclare("patient-processing", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind("patient-processing", "patient.events", "patient.*");

        // Set quality of service
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) => 
        {
            try
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);

                // Deserialize the PatientMessage
                var message = JsonSerializer.Deserialize<PatientMessage>(
                    messageJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                
                if(message != null) 
                {
                    _logger.LogInformation(
                        "Received patient event: {EventType} for patient {PatientId}",
                        message.EventType, message.PatientId);

                    await _processingChannel.WriteAsync(message, stoppingToken);
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                else 
                {
                    _logger.LogWarning("Failed to deserialize PatientMessage: {MessageBody}", messageJson);
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                }
            }
            catch (Exception ex)
            {
                
                _logger.LogError(ex, "Error processing RabbitMQ message");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }

            _channel.BasicConsume("patient-processing", false, consumer);
            _logger.LogInformation("Patient event consumer started");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        };   
    }

    public override async Task StopAsync(CancellationToken cancellationToken) 
    {
        _channel?.Close();
        _connection.Close();
        await base.StopAsync(cancellationToken);
    }
}