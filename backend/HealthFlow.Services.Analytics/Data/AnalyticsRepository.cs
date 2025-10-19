using HealthFlow.Shared.Data;
using HealthFlow.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq.Expressions;
using System.Linq;

namespace HealthFlow.Services.Analytics.Data
{
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly Container _container;
        private readonly ILogger<AnalyticsRepository> _logger;

        public AnalyticsRepository(CosmosClient cosmosClient, string databaseId, string containerId, ILogger<AnalyticsRepository> logger)
        {
            _container = cosmosClient.GetContainer(databaseId, containerId);
            _logger = logger;
        }

        public async Task<IEnumerable<AnalyticsEvent>> GetAsync(Expression<Func<AnalyticsEvent, bool>> predicate)
        {
            try
            {
                var queryable = _container.GetItemLinqQueryable<AnalyticsEvent>();
                var iterator = queryable.Where(predicate).ToFeedIterator();
                var results = new List<AnalyticsEvent>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.Resource);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying Cosmos DB container {ContainerId}", _container.Id);
                throw;
            }
        }

        public async Task<AnalyticsEvent> GetByIdAsync(string id)
        {
            try
            {
                // For Cosmos DB, we need both id and partitionKey
                // Since we're using PatientId as partition key, we need to query instead of direct read
                var query = _container.GetItemLinqQueryable<AnalyticsEvent>()
                    .Where(e => e.Id == id)
                    .Take(1)
                    .ToFeedIterator();

                var response = await query.ReadNextAsync();
                return response.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting analytics event by id: {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsEvent>> GetByPatientAsync(string patientId)
        {
            try
            {
                var query = _container.GetItemLinqQueryable<AnalyticsEvent>()
                    .Where(e => e.PatientId == patientId)
                    .OrderByDescending(e => e.Timestamp)
                    .ToFeedIterator();

                var results = new List<AnalyticsEvent>();
                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting analytics events for patient: {PatientId}", patientId);
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsEvent>> GetByEventTypeAsync(string eventType)
        {
            try
            {
                var query = _container.GetItemLinqQueryable<AnalyticsEvent>()
                    .Where(e => e.EventType == eventType)
                    .OrderByDescending(e => e.Timestamp)
                    .ToFeedIterator();

                var results = new List<AnalyticsEvent>();
                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting analytics events by type: {EventType}", eventType);
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsEvent>> GetByTimeRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var query = _container.GetItemLinqQueryable<AnalyticsEvent>()
                    .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .OrderByDescending(e => e.Timestamp)
                    .ToFeedIterator();

                var results = new List<AnalyticsEvent>();
                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting analytics events by time range: {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsEvent>> GetRecentEventsAsync(int count = 50)
        {
            try
            {
                var query = _container.GetItemLinqQueryable<AnalyticsEvent>()
                    .OrderByDescending(e => e.Timestamp)
                    .Take(count)
                    .ToFeedIterator();

                var response = await query.ReadNextAsync();
                return response.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent analytics events");
                throw;
            }
        }

        public async Task<AnalyticsEvent> AddAsync(AnalyticsEvent analyticsEvent)
        {
            try
            {
                var response = await _container.CreateItemAsync(analyticsEvent, new PartitionKey(analyticsEvent.PatientId));
                _logger.LogDebug("Analytics event added: {EventId} for patient {PatientId}", analyticsEvent.Id, analyticsEvent.PatientId);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding analytics event for patient: {PatientId}", analyticsEvent.PatientId);
                throw;
            }
        }

        public async Task<int> GetEventCountAsync(Expression<Func<AnalyticsEvent, bool>> predicate = null)
        {
            try
            {
                IQueryable<AnalyticsEvent> queryable = _container.GetItemLinqQueryable<AnalyticsEvent>();
                
                if (predicate != null)
                {
                    queryable = queryable.Where(predicate);
                }

                var count = await queryable.CountAsync();
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting analytics events");
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsEvent>> GetPagedEventsAsync(int pageNumber, int pageSize, Expression<Func<AnalyticsEvent, bool>> predicate = null)
        {
            try
            {
                IQueryable<AnalyticsEvent> queryable = _container.GetItemLinqQueryable<AnalyticsEvent>();
                
                if (predicate != null)
                {
                    queryable = queryable.Where(predicate);
                }

                var iterator = queryable
                    .OrderByDescending(e => e.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToFeedIterator();

                var results = new List<AnalyticsEvent>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged analytics events");
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetEventTypeDistributionAsync(DateTime? startDate = null)
        {
            try
            {
                IQueryable<AnalyticsEvent> queryable = _container.GetItemLinqQueryable<AnalyticsEvent>();
                
                if (startDate.HasValue)
                {
                    queryable = queryable.Where(e => e.Timestamp >= startDate.Value);
                }

                // This is a simplified approach - in production you might want to use stored procedures
                // or change feed processor for complex aggregations
                var events = await GetRecentEventsAsync(1000); // Limit for demo purposes
                
                return events
                    .GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event type distribution");
                throw;
            }
        }

        public async Task<int> GetUniquePatientCountAsync(DateTime? startDate = null)
        {
            try
            {
                IQueryable<AnalyticsEvent> queryable = _container.GetItemLinqQueryable<AnalyticsEvent>();
                
                if (startDate.HasValue)
                {
                    queryable = queryable.Where(e => e.Timestamp >= startDate.Value);
                }

                var events = await GetRecentEventsAsync(1000); // Limit for demo purposes
                return events.Select(e => e.PatientId).Distinct().Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unique patient count");
                throw;
            }
        }
    }
}