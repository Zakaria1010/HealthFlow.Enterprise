using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;

namespace HealthFlow.BackgroundWorker.Data;

public class ProcessingRepository : IProcessingRepository, IDisposable
{
    private readonly Container _container;
    private readonly ILogger<ProcessingRepository> _logger;
    private bool _disposed = false;

    public ProcessingRepository(CosmosClient cosmosClient, IOptions<CosmosDbOptions> options, ILogger<ProcessingRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.Value.DatabaseId, "ProcessedEvents");
        _logger = logger;
    }

    public async Task<ProcessedEvent> AddAsync(ProcessedEvent processedEvent)
    {
        try
        {
            var response = await _container.CreateItemAsync(processedEvent, new PartitionKey(processedEvent.PatientId));
            _logger.LogDebug("Processed event stored: {EventId}", processedEvent.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing processed event");
            throw;
        }
    }

    public async Task UpdateAsync(ProcessedEvent processedEvent)
    {
        try
        {
            await _container.ReplaceItemAsync(processedEvent, processedEvent.Id, new PartitionKey(processedEvent.PatientId));
            _logger.LogDebug("Processed event updated: {EventId}", processedEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating processed event: {EventId}", processedEvent.Id);
            throw;
        }
    }

    public async Task<IEnumerable<ProcessedEvent>> GetPendingEventsAsync()
    {
        try
        {
            var query = _container.GetItemLinqQueryable<ProcessedEvent>()
                .Where(e => e.Status == "Pending")
                .OrderBy(e => e.ReceivedAt)
                .Take(100)
                .ToFeedIterator();

            var results = new List<ProcessedEvent>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending events");
            throw;
        }
    }

    public async Task MarkAsProcessedAsync(string eventId)
    {
        try
        {
            // This would need to query by eventId and patientId (partition key)
            // Simplified for example
            var query = _container.GetItemLinqQueryable<ProcessedEvent>()
                .Where(e => e.Id == eventId)
                .Take(1)
                .ToFeedIterator();

            var response = await query.ReadNextAsync();
            var processedEvent = response.FirstOrDefault();

            if (processedEvent != null)
            {
                processedEvent.Status = "Completed";
                processedEvent.ProcessedAt = DateTime.UtcNow;
                await _container.ReplaceItemAsync(processedEvent, processedEvent.Id, new PartitionKey(processedEvent.PatientId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking event as processed: {EventId}", eventId);
            throw;
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        try
        {
            var query = _container.GetItemLinqQueryable<ProcessedEvent>()
                .Where(e => e.Status == "Pending")
                .CountAsync();

            return await query;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending count");
            return 0;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // CosmosClient is disposed by the DI container, so we don't dispose it here
            _disposed = true;
        }
    }
}

public class CosmosDbOptions
{
    public string DatabaseId { get; set; } = "HealthFlowProcessing";
    public string ContainerId { get; set; } = "ProcessedEvents";
}