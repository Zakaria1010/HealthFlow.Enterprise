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
using HealthFlow.Shared.Utils;
using HealthFlow.Shared.Models;

namespace HealthFlow.Services.Analytics.Consumers;
public class AnalyticsEventConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IHubContext<AnalyticsHub> _hubContext;
    private readonly ILogger<AnalyticsEventConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed = false;

    public AnalyticsEventConsumer(
        IServiceProvider serviceProvide,
        IMessagePublisher messagePublisher,
        IHubContext<AnalyticsHub> hubContext,
        ILogger<AnalyticsEventConsumer> logger)
    {
        _serviceProvider = serviceProvide;
        _hubContext = hubContext;
        _logger = logger;
        
        try
        {
            _logger.LogInformation("🔄 Initializing AnalyticsEventConsumer with async mode...");
            // Get connection from the shared publisher
            _connection = messagePublisher.GetConnection();
            _channel = _connection.CreateModel();

            // Declare RabbitMQ exchange and queue for analytics events
            _channel.ExchangeDeclare("analytics.events", "topic", durable: true);
            _channel.QueueDeclare("analytics-processing", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind("analytics-processing", "analytics.events", "analytics.*");
            
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
            _logger.LogInformation("✅ AnalyticsEventConsumer initialized successfully for async mode");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize AnalyticsEventConsumer");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) 
    {
        _logger.LogInformation("🚀 AnalyticsEventConsumer starting in async mode...");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            _logger.LogInformation("📨 Message received from RabbitMQ. DeliveryTag: {DeliveryTag}", ea.DeliveryTag);
            
            try
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);

                _logger.LogInformation("Raw message JSON: {MessageJson}", messageJson);

                var analyticsEvent = JsonSerializer.Deserialize<AnalyticsEvent>(
                    messageJson,
                    new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                
                if (analyticsEvent != null)
                {
                    _logger.LogInformation(
                        "✅ Successfully deserialized analytics event: {EventType} for patient {PatientId}", 
                        analyticsEvent.EventType, analyticsEvent.PatientId);

                    // Process the analytics event
                    await ProcessAnalyticsEvent(analyticsEvent); 
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("✅ Message acknowledged");
                }
                else  {
                    _logger.LogWarning("Failed to deserialize AnalyticsEvent: {MessageJson}", messageJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing analytics event. DeliveryTag: {DeliveryTag}", ea.DeliveryTag);
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                throw;
            }   
        };

        // Start consuming
        var consumerTag = _channel.BasicConsume("analytics-processing", false, consumer);
        _logger.LogInformation("🎯 Analytics async consumer started. ConsumerTag: {ConsumerTag}", consumerTag);

        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("🛑 AnalyticsEventConsumer stopping due to cancellation");
        }
    }

    public async Task ProcessAnalyticsEvent(AnalyticsEvent analyticsEvent)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();
            
            // ✅ Fix: Convert to proper object type based on your JSON structure
            object normalizedPayload = analyticsEvent.Payload switch
            {
                JsonElement jsonElement => JsonSerializer.Deserialize<PatientJson>(jsonElement.GetRawText()),
                string jsonString when JsonUtils.IsJson<string>(jsonString) => JsonSerializer.Deserialize<PatientJson>(jsonString),
                string jsonString => jsonString,
                _ => analyticsEvent.Payload
            };
            
            // Create analytics event and publish to Analytics Service via messaging

            var newAnalyticsEvent = new AnalyticsEvent
            {
                Id = analyticsEvent.Id,
                PatientId = analyticsEvent.PatientId,
                EventType = analyticsEvent.EventType,
                Timestamp = analyticsEvent.Timestamp,
                Payload = normalizedPayload,
                Service = analyticsEvent.Service
            };
            // Store in repository (if needed for additional processing)
            await repository.AddAsync(newAnalyticsEvent);

            // Broadcast real-time update to dashboard clients
            await _hubContext.Clients.All.SendAsync("AnalyticsEventProcessed", new 
            {
                analyticsEvent.Id,
                analyticsEvent.PatientId,
                analyticsEvent.EventType,
                analyticsEvent.Timestamp,
                analyticsEvent.Payload,
                analyticsEvent.Service
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