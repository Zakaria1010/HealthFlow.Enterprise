
using HealthFlow.Shared.Models;
using HealthFlow.Shared.Messaging;

namespace HealthFlow.BackgroundWorker.Services;
public class PatientProcessingService : BackgroundService
{
    private readonly PatientProcessingChannel _channel;
    private readonly IMessagePublisher _messagePublisher;
    // private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<PatientProcessingService> _logger;
    private readonly int _workerCount = 3; // Configurable worker count

    public PatientProcessingService(
        PatientProcessingChannel channel,
        IMessagePublisher messagePublisher,
        // IAnalyticsRepository analyticsRepository,
        ILogger<PatientProcessingService> logger)
    {
        _channel = channel;
        _messagePublisher = messagePublisher;
        // _analyticsRepository = analyticsRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting patient processing service with {WorkerCount} workers", _workerCount);

        var tasks = new List<Task>();
        
        // Start multiple workers for parallel processing
        for (int i = 0; i < _workerCount; i++)
        {
            tasks.Add(Task.Run(() => ProcessMessagesAsync(stoppingToken), stoppingToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
            }
        }
    }

    private async Task ProcessMessageAsync(PatientMessage message, CancellationToken cancellationToken)
    {
        // Simulate processing work
        await Task.Delay(100, cancellationToken);

        // Store in Cosmos DB for analytics
        /*await _analyticsRepository.AddAsync(new AnalyticsEvent
        {
            Id = message.Id.ToString(),
            PatientId = message.PatientId,
            EventType = message.EventType,
            Timestamp = message.Timestamp,
            Payload = message.Payload
        }, cancellationToken); */

        // Publish to RabbitMQ for other services
        await _messagePublisher.PublishAsync("patient.events", message, cancellationToken);

        _logger.LogInformation("Processed message: {MessageId} for patient {PatientId}", 
            message.Id, message.PatientId);
    }
}