using System.Diagnostics;
using HealthFlow.Shared.Models;
using HealthFlow.Shared.Messaging;
using HealthFlow.Shared.Data;
using HealthFlow.BackgroundWorker.Data;

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
            
            // Store in Background Worker's own database for tracking
            var processedEvent = new ProcessedEvent
            {
                OriginalMessageId = message.Id.ToString(),
                PatientId = message.PatientId,
                EventType = message.EventType,
                ReceivedAt = DateTime.UtcNow,
                Status = "Processing",
                Payload = message.Payload,
                RetryCount = 0
            };

            await _processingRepository.AddAsync(processedEvent);

            // Store in Cosmos DB for analytics
            var analyticsEvent = AnalyticsEvent.CreatePatientEvent(
                message.PatientId,
                "PatientDataProcessed",
                "BackgroundWorker",
                message.Payload);

            await _messagePublisher.PublishAsync(
                "analytics.events",
                "analytics.event.processed",
                analyticsEvent, 
                cancellationToken);
            
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