using HealthFlow.Shared.Messaging;
using HealthFlow.Shared.Models;
using HealthFlow.Services.Analytics.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using HealthFlow.Shared.Data;

namespace HealthFlow.Services.Analytics.Consumers;
public class AnalyticsEventConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IHubContext<AnalyticsHub> _hubContext;
    private readonly ILogger<AnalyticsEventConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AnalyticsEventConsumer(
        IServiceProvider serviceProvide,
        IMessagePublisher messagePublisher,
        IHubContext<AnalyticsHub> hubContext,
        ILogger<AnalyticsEventConsumer> logger)
    {
        _serviceProvider = serviceProvide;
        _hubContext = hubContext;
        _logger = logger;
        
        // Get connection from the shared publisher
        _connection = messagePublisher.GetConnection();
        _channel = _connection.CreateModel();

        // Declare RabbitMQ exchange and queue for analytics events
        _channel.ExchangeDeclare("analytics.events", ExchangeType.Topic, durable: true);
        _channel.QueueDeclare("analytics-processing", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind("analytics-processing", "analytics.events", "analytics.*");
        
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

                var analyticsEvent = JsonSerializer.Deserialize<AnalyticsEvent>(
                    messageJson,
                    new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                
                if (analyticsEvent != null)
                {
                    _logger.LogInformation(
                        "Received analytics event: {EventType} for patient {PatientId}", 
                        analyticsEvent.EventType, analyticsEvent.PatientId);

                    // Process the analytics event
                    await ProcessAnalyticsEvent(analyticsEvent); 
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                else  {
                    _logger.LogWarning("Failed to deserialize AnalyticsEvent: {MessageJson}", messageJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing analytics event");
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                throw;
            }
        };
    }

    public async Task ProcessAnalyticsEvent(AnalyticsEvent analyticsEvent)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();
            // Store in repository (if needed for additional processing)
            await repository.AddAsync(analyticsEvent);

            // Broadcast real-time update to dashboard clients
            await _hubContext.Clients.All.SendAsync("AnalyticsEventProcessed", 
            new {
                analyticsEvent.EventType,
                analyticsEvent.PatientId,
                analyticsEvent.Timestamp,
                analyticsEvent.Payload
            });
            _logger.LogInformation(
                "Processed and broadcasted analytics event: {EventType} for patient {PatientId}", 
                analyticsEvent.EventType, analyticsEvent.PatientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analytics event: {EventId}", analyticsEvent.Id);
            throw;
        }
    }

    public async override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Analytics Event Consumer...");
        _channel.Close();
        _connection.Close();
        await base.StopAsync(cancellationToken);
    }

}