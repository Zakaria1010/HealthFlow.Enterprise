using HealthFlow.Services.Analytics.Hubs;
using HealthFlow.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using HealthFlow.Services.Analytics.Models;
using HealthFlow.Shared.Data;

namespace HealthFlow.Services.Analytics.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IRepository<AnalyticsEvent> _repository;
        private readonly IHubContext<AnalyticsHub> _hubContext;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            IRepository<AnalyticsEvent> repository,
            IHubContext<AnalyticsHub> hubContext,
            ILogger<AnalyticsController> logger)
        {
            _repository = repository;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost("events")]
        public async Task<IActionResult> ProcessEvent([FromBody] AnalyticsEvent analyticsEvent)
        {
            try
            {
                // Validate the event
                if (string.IsNullOrEmpty(analyticsEvent.PatientId) || string.IsNullOrEmpty(analyticsEvent.EventType))
                {
                    return BadRequest("PatientId and EventType are required");
                }

                // Set timestamp if not provided
                if (analyticsEvent.Timestamp == default)
                {
                    analyticsEvent.Timestamp = DateTime.UtcNow;
                }

                // Store in Cosmos DB
                await _repository.AddAsync(analyticsEvent);
                
                // Broadcast to all connected dashboard clients
                await _hubContext.Clients.All.SendAsync("EventProcessed", new
                {
                    id = analyticsEvent.Id,
                    patientId = analyticsEvent.PatientId,
                    eventType = analyticsEvent.EventType,
                    timestamp = analyticsEvent.Timestamp,
                    payload = analyticsEvent.Payload,
                    service = analyticsEvent.Service
                });

                // Send to specific patient group if applicable
                if (analyticsEvent.PatientId != "system")
                {
                    await _hubContext.Clients.Group($"patient-{analyticsEvent.PatientId}")
                        .SendAsync("PatientEvent", new
                        {
                            patientId = analyticsEvent.PatientId,
                            eventType = analyticsEvent.EventType,
                            timestamp = analyticsEvent.Timestamp,
                            payload = analyticsEvent.Payload
                        });
                }

                // Send live metrics update for dashboard
                await SendLiveMetricsUpdate();

                _logger.LogInformation("Analytics event processed: {EventId} of type {EventType} for patient {PatientId}", 
                    analyticsEvent.Id, analyticsEvent.EventType, analyticsEvent.PatientId);
                
                return Ok(new { message = "Event processed successfully", eventId = analyticsEvent.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing analytics event");
                return StatusCode(500, "Error processing event");
            }
        }

        [HttpGet("events")]
        public async Task<ActionResult<IEnumerable<AnalyticsEvent>>> GetEvents(
            [FromQuery] string? patientId = null,
            [FromQuery] string? eventType = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _repository.GetAsync(e => true); // Start with all events

                // Apply filters
                if (!string.IsNullOrEmpty(patientId))
                {
                    query = _repository.GetAsync(e => e.PatientId == patientId);
                }

                if (!string.IsNullOrEmpty(eventType))
                {
                    query = _repository.GetAsync(e => e.EventType == eventType);
                }

                if (startDate.HasValue)
                {
                    query = _repository.GetAsync(e => e.Timestamp >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = _repository.GetAsync(e => e.Timestamp <= endDate.Value);
                }

                var events = (await query).ToList();
                
                // Apply pagination
                var totalCount = events.Count;
                var pagedEvents = events
                    .OrderByDescending(e => e.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                Response.Headers.Add("X-Total-Count", totalCount.ToString());
                Response.Headers.Add("X-Page", page.ToString());
                Response.Headers.Add("X-Page-Size", pageSize.ToString());

                return Ok(pagedEvents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events");
                return StatusCode(500, "Error retrieving events");
            }
        }

        [HttpGet("events/{id}")]
        public async Task<ActionResult<AnalyticsEvent>> GetEvent(string id)
        {
            try
            {
                var events = await _repository.GetAsync(e => e.Id == id);
                var analyticsEvent = events.FirstOrDefault();
                
                if (analyticsEvent == null)
                {
                    return NotFound();
                }
                
                return analyticsEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving event {EventId}", id);
                return StatusCode(500, "Error retrieving event");
            }
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardData>> GetDashboardData([FromQuery] DateTime? startDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow.AddDays(-7);
                
                var events = (await _repository.GetAsync(e => e.Timestamp >= startDate.Value)).ToList();

                var dashboardData = new DashboardData
                {
                    TotalPatients = events.Where(e => e.PatientId != "system").Select(e => e.PatientId).Distinct().Count(),
                    AverageWaitTime = CalculateAverageWaitTime(events),
                    CriticalPatients = events.Count(e => e.EventType == EventTypes.PatientCritical),
                    AdmittedToday = events.Count(e => 
                        e.EventType == EventTypes.PatientAdmitted && 
                        e.Timestamp.Date == DateTime.UtcNow.Date),
                    TotalEvents = events.Count,
                    EventsByType = events.GroupBy(e => e.EventType)
                                        .ToDictionary(g => g.Key, g => g.Count()),
                    PatientStatusDistribution = await GetPatientStatusDistribution(events),
                    HourlyAdmissions = GenerateHourlyAdmissions(events),
                    WaitTimeTrend = GenerateWaitTimeTrend(events),
                    SystemHealth = await GetSystemHealth()
                };

                _logger.LogInformation("Dashboard data generated for {EventCount} events since {StartDate}", 
                    events.Count, startDate);

                return dashboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating dashboard data");
                return StatusCode(500, "Error generating dashboard data");
            }
        }

        [HttpGet("patients/{patientId}/events")]
        public async Task<ActionResult<IEnumerable<AnalyticsEvent>>> GetPatientEvents(string patientId)
        {
            try
            {
                var events = await _repository.GetAsync(e => e.PatientId == patientId);
                return Ok(events.OrderByDescending(e => e.Timestamp));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events for patient {PatientId}", patientId);
                return StatusCode(500, "Error retrieving patient events");
            }
        }

        [HttpPost("broadcast")]
        public async Task<IActionResult> BroadcastMessage([FromBody] BroadcastMessageRequest request)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("BroadcastMessage", new
                {
                    message = request.Message,
                    type = request.Type,
                    timestamp = DateTime.UtcNow,
                    from = "System"
                });

                _logger.LogInformation("Broadcast message sent: {Message}", request.Message);
                return Ok(new { message = "Broadcast sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message");
                return StatusCode(500, "Error broadcasting message");
            }
        }

        private async Task SendLiveMetricsUpdate()
        {
            try
            {
                var recentEvents = (await _repository.GetAsync(e => e.Timestamp >= DateTime.UtcNow.AddHours(-1))).ToList();
                
                var liveMetrics = new
                {
                    eventsLastHour = recentEvents.Count,
                    criticalPatients = recentEvents.Count(e => e.EventType == EventTypes.PatientCritical),
                    uniquePatients = recentEvents.Where(e => e.PatientId != "system").Select(e => e.PatientId).Distinct().Count(),
                    timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("LiveMetricsUpdate", liveMetrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending live metrics update");
            }
        }

        private async Task<Dictionary<string, int>> GetPatientStatusDistribution(List<AnalyticsEvent> events)
        {
            // This would typically call the Patient Service in a real implementation
            // For now, we'll simulate based on recent events
            return new Dictionary<string, int>
            {
                { "Admitted", events.Count(e => e.EventType == EventTypes.PatientAdmitted) + 25 },
                { "InTreatment", events.Count(e => e.EventType == EventTypes.PatientStatusChanged) + 18 },
                { "Discharged", events.Count(e => e.EventType == EventTypes.PatientDischarged) + 12 },
                { "Critical", events.Count(e => e.EventType == EventTypes.PatientCritical) + 3 },
                { "Waiting", 7 }
            };
        }

        private List<HourlyAdmission> GenerateHourlyAdmissions(List<AnalyticsEvent> events)
        {
            var todayAdmissions = events
                .Where(e => e.EventType == EventTypes.PatientAdmitted && e.Timestamp.Date == DateTime.UtcNow.Date)
                .GroupBy(e => e.Timestamp.Hour)
                .Select(g => new HourlyAdmission
                {
                    Hour = $"{g.Key:00}:00",
                    Admissions = g.Count()
                })
                .ToList();

            // Fill in missing hours
            for (int hour = 0; hour < 24; hour++)
            {
                if (!todayAdmissions.Any(a => a.Hour == $"{hour:00}:00"))
                {
                    todayAdmissions.Add(new HourlyAdmission { Hour = $"{hour:00}:00", Admissions = 0 });
                }
            }

            return todayAdmissions.OrderBy(a => a.Hour).ToList();
        }

        private List<WaitTimeTrend> GenerateWaitTimeTrend(List<AnalyticsEvent> events)
        {
            var random = new Random();
            var trend = new List<WaitTimeTrend>();
            var baseDate = DateTime.UtcNow.AddDays(-7);
            
            for (int i = 0; i < 7; i++)
            {
                var date = baseDate.AddDays(i);
                var dateEvents = events.Where(e => e.Timestamp.Date == date.Date).ToList();
                
                trend.Add(new WaitTimeTrend
                {
                    Date = date.ToString("MMM dd"),
                    WaitTime = dateEvents.Any() ? dateEvents.Average(_ => random.Next(20, 60)) : random.Next(25, 45)
                });
            }
            
            return trend;
        }

        private async Task<SystemHealth> GetSystemHealth()
        {
            // In a real implementation, this would check actual service health
            return new SystemHealth
            {
                PatientService = "Healthy",
                AnalyticsService = "Healthy",
                BackgroundWorker = "Processing",
                Database = "Connected",
                MessageQueue = "Active",
                LastChecked = DateTime.UtcNow
            };
        }

        private static double CalculateAverageWaitTime(List<AnalyticsEvent> events)
        {
            // Simulate wait time calculation based on event types and timestamps
            var random = new Random();
            return events.Any() ? events.Average(_ => random.Next(15, 120)) : 35.5;
        }
    }

    public class BroadcastMessageRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "Info"; // Info, Warning, Error
    }
}