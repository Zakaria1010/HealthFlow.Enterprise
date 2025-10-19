using HealthFlow.Shared.Models;
using System.Linq.Expressions;

namespace HealthFlow.Shared.Data;
public interface IAnalyticsRepository
{
    Task<AnalyticsEvent> GetByIdAsync(string id);
    Task<IEnumerable<AnalyticsEvent>> GetByPatientAsync(string patientId);
    Task<IEnumerable<AnalyticsEvent>> GetByEventTypeAsync(string eventType);
    Task<IEnumerable<AnalyticsEvent>> GetByTimeRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<AnalyticsEvent>> GetRecentEventsAsync(int count = 50);
    Task<AnalyticsEvent> AddAsync(AnalyticsEvent analyticsEvent);
    Task<IEnumerable<AnalyticsEvent>> GetAsync(Expression<Func<AnalyticsEvent, bool>> predicate);
    Task<int> GetEventCountAsync(Expression<Func<AnalyticsEvent, bool>> predicate = null);
    Task<IEnumerable<AnalyticsEvent>> GetPagedEventsAsync(int pageNumber, int pageSize, Expression<Func<AnalyticsEvent, bool>> predicate = null);
    Task<Dictionary<string, int>> GetEventTypeDistributionAsync(DateTime? startDate = null);
    Task<int> GetUniquePatientCountAsync(DateTime? startDate = null);
}