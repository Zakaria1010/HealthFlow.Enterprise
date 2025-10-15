using System.Linq.Expressions;

namespace HealthFlow.Shared.Data
{
    public interface IRepository<T> where T : class
    {
        Task<T> GetByIdAsync(string id, string partitionKey = null);
        Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task<T> UpdateAsync(T entity);
        Task DeleteAsync(string id, string partitionKey = null);
        Task<bool> ExistsAsync(string id, string partitionKey = null);
        Task<int> CountAsync(Expression<Func<T, bool>> predicate = null);
        Task<IEnumerable<T>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate = null);
    }
}