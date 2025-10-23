using System.Diagnostics;
using HealthFlow.Shared.Models;
using System.Text.Json.Serialization;
using HealthFlow.Shared.Messaging;
using HealthFlow.Shared.Data;
using HealthFlow.BackgroundWorker.Data;
using System.Text.Json; 
using HealthFlow.Shared.Utils;

namespace HealthFlow.BackgroundWorker.Services;
public class PatientProcessingService : BackgroundService
{
    private readonly PatientProcessingChannel _channel;
    private readonly IProcessingRepository _processingRepository;
    private readonly IMessagePublisher _messagePublisher;   
    private readonly ILogger<PatientProcessingService> _logger;
    private readonly int _workerCount = 3; // Configurable worker count
    private readonly SemaphoreSlim _semaphore;


    public PatientProcessingService(
        PatientProcessingChannel channel,
        IMessagePublisher messagePublisher,
        ILogger<PatientProcessingService> logger,
        IProcessingRepository processingRepository)
    {
        _channel = channel;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _processingRepository = processingRepository;
        _semaphore = new SemaphoreSlim(_workerCount, _workerCount);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting patient processing service with {WorkerCount} workers", _workerCount);

        var tasks = new List<Task>();
        
        // Start multiple workers for parallel processing
        for (int i = 0; i < _workerCount; i++)
        {
            tasks.Add(Task.Run(() => ProcessMessagesAsync($"Worker-{i + 1}", stoppingToken), stoppingToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessMessagesAsync(string WorkerName, CancellationToken stoppingToken)
    {
        await foreach (var message in _channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(message, WorkerName, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
            }
        }
    }

    private async Task ProcessMessageAsync(PatientMessage message, string workerName, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
       try
       {
            _logger.LogInformation("Worker {WorkerName} processing message {MessageId} for patient {PatientId}", workerName, message.Id, message.PatientId);
            
            // Log the payload type and content
            _logger.LogInformation("Payload type: {PayloadType}, Content: {PayloadJson}", 
                message.Payload?.GetType().Name ?? "null",
                System.Text.Json.JsonSerializer.Serialize(message.Payload, new JsonSerializerOptions { WriteIndented = true }));

            // ✅ Fix: Convert to proper object type based on your JSON structure
            object normalizedPayload = message.Payload switch
            {
                JsonElement jsonElement => JsonSerializer.Deserialize<PatientJson>(jsonElement.GetRawText()),
                string jsonString when JsonUtils.IsJson<string>(jsonString) => JsonSerializer.Deserialize<PatientJson>(jsonString),
                string jsonString => jsonString,
                _ => message.Payload
            };

            // Store in Background Worker's own database for tracking
            var processedEvent = new ProcessedEvent
            {
                OriginalMessageId = message.Id.ToString(),
                PatientId = message.PatientId,
                EventType = message.EventType,
                ReceivedAt = DateTime.UtcNow,
                Status = "Processing",
                Payload = normalizedPayload,
                RetryCount = 0
            };

            await _processingRepository.AddAsync(processedEvent);

            _logger.LogInformation("✅ Stored processed event {ProcessedEventId} in BackgroundWorker database", processedEvent.Id);
            
            // Create analytics event and publish to Analytics Service via messaging
            var analyticsEvent = new AnalyticsEvent
            {
                Id = Guid.NewGuid().ToString(),
                PatientId = message.PatientId,
                EventType = message.EventType,
                Timestamp = message.Timestamp,
                Payload = normalizedPayload,
                Service = "BackgroundWorker",
                CorrelationId = message.CorrelationId
            };

            await _messagePublisher.PublishAsync(
                "analytics.events",
                "analytics.event.processed",
                analyticsEvent, 
                cancellationToken);

            _logger.LogInformation("✅ Published analytics event to analytics.events with routing key: analytics.event.processed");
            
            // Mark as completed
            await _processingRepository.MarkAsProcessedAsync(processedEvent.Id);

            // Simulate processing work
            await Task.Delay(Random.Shared.Next(50, 200), cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("{WorkerName} completed message {MessageId} in {ElapsedMs}ms", 
                workerName, message.Id, stopwatch.ElapsedMilliseconds);
       }
       catch (Exception ex)
       {      
            _logger.LogError(ex, "{WorkerName} error processing message {MessageId}", workerName, message.Id);
       }
    }
}


