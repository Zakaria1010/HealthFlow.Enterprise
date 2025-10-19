using HealthFlow.Shared.Models;

namespace HealthFlow.BackgroundWorker.Data
{
    public interface IProcessingRepository
    {
        Task<ProcessedEvent> AddAsync(ProcessedEvent processedEvent);
        Task<IEnumerable<ProcessedEvent>> GetPendingEventsAsync();
        Task MarkAsProcessedAsync(string eventId);
        Task<int> GetPendingCountAsync();
    }

    public class ProcessedEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OriginalMessageId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
        public object Payload { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public int RetryCount { get; set; } = 0;

        // Add this property for Cosmos DB partition key
        public string PartitionKey => PatientId; // This maps to /partitionKey in Cosmos DB
    }
}