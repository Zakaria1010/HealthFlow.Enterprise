using HealthFlow.Shared.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq.Expressions;

namespace HealthFlow.Services.Analytics.Data
{
    public class CosmosRepository<T> : IRepository<T> where T : class
    {
        private readonly Container _container;
        private readonly string _databaseId;
        private readonly string _containerId;
        private readonly ILogger<CosmosRepository<T>> _logger;

        public CosmosRepository(
            CosmosClient cosmosClient,
            string databaseId,
            string containerId,
            ILogger<CosmosRepository<T>> logger)
        {
            _databaseId = databaseId;
            _containerId = containerId;
            _container = cosmosClient.GetContainer(databaseId, containerId);
            _logger = logger;
        }

        public async Task<T> GetByIdAsync(string id, string partitionKey = null)
        {
            try
            {
                ItemResponse<T> response = partitionKey != null
                    ? await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey))
                    : await _container.ReadItemAsync<T>(id, PartitionKey.None);

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Item with id {Id} not found in container {ContainerId}", id, _containerId);
                return null;
            }
        }

        public async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                var queryable = _container.GetItemLinqQueryable<T>();
                var iterator = queryable.Where(predicate).ToFeedIterator();
                var results = new List<T>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.Resource);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying Cosmos DB container {ContainerId}", _containerId);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                var query = _container.GetItemQueryIterator<T>();
                var results = new List<T>();

                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    results.AddRange(response.ToList());
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all items from Cosmos DB container {ContainerId}", _containerId);
                throw;
            }
        }

        public async Task<T> AddAsync(T entity)
        {
            try
            {
                var response = await _container.CreateItemAsync(entity);
                _logger.LogDebug("Item added to Cosmos DB: {Id}", GetIdFromEntity(entity));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to Cosmos DB container {ContainerId}", _containerId);
                throw;
            }
        }

        public async Task<T> UpdateAsync(T entity)
        {
            try
            {
                var id = GetIdFromEntity(entity);
                var partitionKey = GetPartitionKeyFromEntity(entity);
                
                var response = partitionKey != null
                    ? await _container.ReplaceItemAsync(entity, id, new PartitionKey(partitionKey))
                    : await _container.ReplaceItemAsync(entity, id);

                _logger.LogDebug("Item updated in Cosmos DB: {Id}", id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item in Cosmos DB container {ContainerId}", _containerId);
                throw;
            }
        }

        public async Task DeleteAsync(string id, string partitionKey = null)
        {
            try
            {
                if (partitionKey != null)
                {
                    await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));
                }
                else
                {
                    await _container.DeleteItemAsync<T>(id, PartitionKey.None);
                }

                _logger.LogDebug("Item deleted from Cosmos DB: {Id}", id);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Item with id {Id} not found for deletion in container {ContainerId}", id, _containerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item from Cosmos DB container {ContainerId}", _containerId);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string id, string partitionKey = null)
        {
            try
            {
                await GetByIdAsync(id, partitionKey);
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null)
        {
            try
            {
                IQueryable<T> queryable = _container.GetItemLinqQueryable<T>();
                
                if (predicate != null)
                {
                    queryable = queryable.Where(predicate);
                }

                var count = await queryable.CountAsync();
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting items in Cosmos DB container {ContainerId}", _containerId);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate = null)
        {
            try
            {
                IQueryable<T> queryable = _container.GetItemLinqQueryable<T>();
                
                if (predicate != null)
                {
                    queryable = queryable.Where(predicate);
                }

                var iterator = queryable
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToFeedIterator();

                var results = new List<T>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.Resource);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged items from Cosmos DB container {ContainerId}", _containerId);
                throw;
            }
        }

        private string GetIdFromEntity(T entity)
        {
            var property = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("id");
            return property?.GetValue(entity)?.ToString();
        }

        private string GetPartitionKeyFromEntity(T entity)
        {
            var property = typeof(T).GetProperty("PartitionKey") ?? 
                          typeof(T).GetProperty("partitionKey") ??
                          typeof(T).GetProperty("PatientId"); // Fallback to PatientId for AnalyticsEvent
            
            return property?.GetValue(entity)?.ToString();
        }
    }
}