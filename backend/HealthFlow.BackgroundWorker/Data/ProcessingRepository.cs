using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using System.Net;

namespace HealthFlow.BackgroundWorker.Data;

public class ProcessingRepository : IProcessingRepository, IDisposable
{
    private  Container _container;
    private Database _database;
    private readonly ILogger<ProcessingRepository> _logger;
    private bool _disposed = false;

    public ProcessingRepository(CosmosClient cosmosClient, IOptions<CosmosDbOptions> options, ILogger<ProcessingRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeAsync(cosmosClient, options).GetAwaiter().GetResult(); // sync-over-async only in constructor
    }

    private async Task InitializeAsync(CosmosClient cosmosClient, IOptions<CosmosDbOptions> options)
    {
        if (cosmosClient == null) throw new ArgumentNullException(nameof(cosmosClient));
        if (options?.Value == null) throw new ArgumentNullException(nameof(options));

        // Create or get database with database-level throughput
        var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
            options.Value.DatabaseId,
            throughput: 1000 // total RU/s for all containers
        );
        _database = databaseResponse.Database;

        // Create or get container without specifying per-container throughput
        var containerProperties = new ContainerProperties
        {
            Id = options.Value.ContainerId,       // e.g., "ProcessedEvents"
            PartitionKeyPath = "/patientId"       // must match property in object
        };

        var containerResponse = await _database.CreateContainerIfNotExistsAsync(containerProperties);
        _container = containerResponse.Container;

        _logger.LogInformation("Cosmos DB initialized. Database: {DatabaseId}, Container: {ContainerId}",
            options.Value.DatabaseId, options.Value.ContainerId);
    }

    public async Task<ProcessedEvent> AddAsync(ProcessedEvent processedEvent)
    {
        if (processedEvent == null)
            throw new ArgumentNullException(nameof(processedEvent));

        if (string.IsNullOrWhiteSpace(processedEvent.PatientId))
            throw new ArgumentException("PatientId (partition key) cannot be null or empty.", nameof(processedEvent.PatientId));

        try
        {
            _logger.LogDebug("Attempting to store processed event: {EventId} with PartitionKey={PartitionKey}", 
                processedEvent.Id, processedEvent.PatientId);

            // Optional: verify container exists
            try
            {
                await _container.ReadContainerAsync();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Container {ContainerId} not found. Re-initializing...", _container.Id);

                var containerProperties = new ContainerProperties
                {
                    Id = _container.Id,
                    PartitionKeyPath = "/patientId"
                };

                var containerResponse = await _database.CreateContainerIfNotExistsAsync(containerProperties);
                _container = containerResponse.Container;

                _logger.LogInformation("Container {ContainerId} re-initialized successfully.", _container.Id);
            }

            var response = await _container.CreateItemAsync(processedEvent, new PartitionKey(processedEvent.PatientId));
            _logger.LogDebug("Processed event successfully stored: {EventId}", processedEvent.Id);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Database or container not found for event {EventId}", processedEvent.Id);
            throw;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error storing processed event {EventId}", processedEvent.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error storing processed event {EventId}", processedEvent.Id);
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