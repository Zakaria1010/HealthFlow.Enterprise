using Microsoft.AspNetCore.SignalR;
using HealthFlow.Services.Patients.Models;
using HealthFlow.Services.Patients.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthFlow.Services.Patients.Hubs
{
    public partial class PatientHub : Hub
    {
        private readonly ILogger<PatientHub> _logger;
        private readonly PatientDbContext _context;
        private static int _connectedClients = 0;
        private static readonly Dictionary<string, string> _userConnections = new();
        private static readonly Dictionary<string, List<string>> _patientSubscriptions = new();

        public PatientHub(ILogger<PatientHub> logger, PatientDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            Interlocked.Increment(ref _connectedClients);
            _logger.LogInformation("Client connected to PatientHub: {ConnectionId}. Total clients: {ConnectedClients}", 
                Context.ConnectionId, _connectedClients);

            // Store user connection
            _userConnections[Context.ConnectionId] = Context.User?.Identity?.Name ?? "Anonymous";

            // Send connection confirmation
            await Clients.Caller.SendAsync("ConnectionEstablished", new 
            {
                ConnectionId = Context.ConnectionId,
                Message = "Connected to HealthFlow Patient Hub",
                ServerTime = DateTime.UtcNow,
                TotalConnections = _connectedClients
            });

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Interlocked.Decrement(ref _connectedClients);
            
            // Remove user connection
            _userConnections.Remove(Context.ConnectionId);
            
            // Remove all subscriptions for this connection
            foreach (var patientSubscriptions in _patientSubscriptions)
            {
                patientSubscriptions.Value.Remove(Context.ConnectionId);
            }

            _logger.LogInformation("Client disconnected from PatientHub: {ConnectionId}. Total clients: {ConnectedClients}", 
                Context.ConnectionId, _connectedClients);

            await base.OnDisconnectedAsync(exception);
        }

        // Client can subscribe to receive updates for a specific patient
        public async Task SubscribeToPatient(string patientId)
        {
            try
            {
                // Verify patient exists
                var patientExists = await _context.Patients.AnyAsync(p => p.Id == Guid.Parse(patientId));
                if (!patientExists)
                {
                    await Clients.Caller.SendAsync("SubscriptionError", new 
                    {
                        PatientId = patientId,
                        Message = "Patient not found"
                    });
                    return;
                }

                // Add to group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{patientId}");
                
                // Track subscription
                if (!_patientSubscriptions.ContainsKey(patientId))
                {
                    _patientSubscriptions[patientId] = new List<string>();
                }
                _patientSubscriptions[patientId].Add(Context.ConnectionId);

                _logger.LogInformation("Client {ConnectionId} subscribed to patient {PatientId}", 
                    Context.ConnectionId, patientId);

                await Clients.Caller.SendAsync("SubscribedToPatient", new 
                {
                    PatientId = patientId,
                    Message = $"Subscribed to patient {patientId}",
                    SubscribedAt = DateTime.UtcNow
                });

                // Send current patient data
                var patient = await _context.Patients
                    .FirstOrDefaultAsync(p => p.Id == Guid.Parse(patientId));
                
                if (patient != null)
                {
                    await Clients.Caller.SendAsync("PatientData", new 
                    {
                        Patient = patient,
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing client {ConnectionId} to patient {PatientId}", 
                    Context.ConnectionId, patientId);
                
                await Clients.Caller.SendAsync("SubscriptionError", new 
                {
                    PatientId = patientId,
                    Message = "Error subscribing to patient"
                });
            }
        }

        // Client can unsubscribe from a patient
        public async Task UnsubscribeFromPatient(string patientId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"patient-{patientId}");
                
                // Remove from tracking
                if (_patientSubscriptions.ContainsKey(patientId))
                {
                    _patientSubscriptions[patientId].Remove(Context.ConnectionId);
                }

                _logger.LogInformation("Client {ConnectionId} unsubscribed from patient {PatientId}", 
                    Context.ConnectionId, patientId);

                await Clients.Caller.SendAsync("UnsubscribedFromPatient", new 
                {
                    PatientId = patientId,
                    Message = $"Unsubscribed from patient {patientId}",
                    UnsubscribedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing client {ConnectionId} from patient {PatientId}", 
                    Context.ConnectionId, patientId);
            }
        }

        // Client can subscribe to all patient updates
        public async Task SubscribeToAllPatients()
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "all-patients");
                
                _logger.LogInformation("Client {ConnectionId} subscribed to all patients", Context.ConnectionId);

                await Clients.Caller.SendAsync("SubscribedToAllPatients", new 
                {
                    Message = "Subscribed to all patient updates",
                    SubscribedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing client {ConnectionId} to all patients", Context.ConnectionId);
            }
        }

        // Client can request current patient list
        public async Task RequestPatientList()
        {
            try
            {
                var patients = await _context.Patients
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(50) // Limit for performance
                    .ToListAsync();

                await Clients.Caller.SendAsync("PatientList", new 
                {
                    Patients = patients,
                    TotalCount = patients.Count,
                    LastUpdated = DateTime.UtcNow
                });

                _logger.LogDebug("Sent patient list to client {ConnectionId}", Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending patient list to client {ConnectionId}", Context.ConnectionId);
                
                await Clients.Caller.SendAsync("Error", new 
                {
                    Message = "Error retrieving patient list"
                });
            }
        }

        // Client can request patient statistics
        public async Task RequestPatientStats()
        {
            try
            {
                var stats = new
                {
                    TotalPatients = await _context.Patients.CountAsync(),
                    AdmittedCount = await _context.Patients.CountAsync(p => p.Status == PatientStatus.Admitted),
                    InTreatmentCount = await _context.Patients.CountAsync(p => p.Status == PatientStatus.InTreatment),
                    CriticalCount = await _context.Patients.CountAsync(p => p.Status == PatientStatus.Critical),
                    DischargedCount = await _context.Patients.CountAsync(p => p.Status == PatientStatus.Discharged),
                    AverageAge = await _context.Patients.AverageAsync(p => p.Age),
                    LastUpdated = DateTime.UtcNow
                };

                await Clients.Caller.SendAsync("PatientStats", stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending patient stats to client {ConnectionId}", Context.ConnectionId);
            }
        }

        // Client can send a command (for authorized users)
        public async Task SendCommand(string command, object data)
        {
            try
            {
                _logger.LogInformation("Client {ConnectionId} sent command: {Command}", Context.ConnectionId, command);

                // Here you could implement command processing logic
                // For now, just echo back with processing status
                await Clients.Caller.SendAsync("CommandProcessed", new 
                {
                    Command = command,
                    Status = "Received",
                    ProcessedAt = DateTime.UtcNow,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command from client {ConnectionId}", Context.ConnectionId);
                
                await Clients.Caller.SendAsync("CommandError", new 
                {
                    Command = command,
                    Error = "Error processing command",
                    Data = data
                });
            }
        }

        // Ping method for connection testing
        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", new 
            {
                ServerTime = DateTime.UtcNow,
                Message = "PatientHub is alive and well!"
            });
        }

        // Get connection info
        public async Task GetConnectionInfo()
        {
            var info = new
            {
                ConnectionId = Context.ConnectionId,
                User = Context.User?.Identity?.Name ?? "Anonymous",
                ConnectedAt = Context.GetHttpContext()?.Connection.Id,
                TotalConnections = _connectedClients,
                PatientSubscriptions = _patientSubscriptions.Count(p => p.Value.Contains(Context.ConnectionId)),
                ServerTime = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("ConnectionInfo", info);
        }
    }

    // Extension methods for broadcasting from other parts of the application
    public static class PatientHubExtensions
    {
        // Broadcast patient creation to all subscribed clients
        public static async Task BroadcastPatientCreated(this IHubContext<PatientHub> hubContext, Patient patient)
        {
            await hubContext.Clients.All.SendAsync("PatientCreated", new
            {
                Patient = patient,
                CreatedAt = DateTime.UtcNow,
                EventType = "PatientCreated"
            });

            // Also send to patient-specific group
            await hubContext.Clients.Group($"patient-{patient.Id}").SendAsync("PatientCreated", new
            {
                Patient = patient,
                CreatedAt = DateTime.UtcNow,
                EventType = "PatientCreated"
            });
        }

        // Broadcast patient update to all subscribed clients
        public static async Task BroadcastPatientUpdated(this IHubContext<PatientHub> hubContext, Patient patient)
        {
            await hubContext.Clients.All.SendAsync("PatientUpdated", new
            {
                Patient = patient,
                UpdatedAt = DateTime.UtcNow,
                EventType = "PatientUpdated"
            });

            await hubContext.Clients.Group($"patient-{patient.Id}").SendAsync("PatientUpdated", new
            {
                Patient = patient,
                UpdatedAt = DateTime.UtcNow,
                EventType = "PatientUpdated"
            });
        }

        // Broadcast patient status change
        public static async Task BroadcastPatientStatusChanged(this IHubContext<PatientHub> hubContext, 
            Guid patientId, PatientStatus oldStatus, PatientStatus newStatus)
        {
            var message = new
            {
                PatientId = patientId,
                OldStatus = oldStatus.ToString(),
                NewStatus = newStatus.ToString(),
                ChangedAt = DateTime.UtcNow,
                EventType = "PatientStatusChanged"
            };

            await hubContext.Clients.All.SendAsync("PatientStatusChanged", message);
            await hubContext.Clients.Group($"patient-{patientId}").SendAsync("PatientStatusChanged", message);

            // If status is critical, send urgent notification
            if (newStatus == PatientStatus.Critical)
            {
                await hubContext.Clients.All.SendAsync("CriticalAlert", new
                {
                    PatientId = patientId,
                    Message = $"Patient {patientId} is in critical condition",
                    AlertLevel = "High",
                    TriggeredAt = DateTime.UtcNow
                });
            }
        }

        // Broadcast patient deletion
        public static async Task BroadcastPatientDeleted(this IHubContext<PatientHub> hubContext, Guid patientId, string patientName)
        {
            var message = new
            {
                PatientId = patientId,
                PatientName = patientName,
                DeletedAt = DateTime.UtcNow,
                EventType = "PatientDeleted"
            };

            await hubContext.Clients.All.SendAsync("PatientDeleted", message);
            await hubContext.Clients.Group($"patient-{patientId}").SendAsync("PatientDeleted", message);
        }

        // Broadcast system-wide notification
        public static async Task BroadcastSystemNotification(this IHubContext<PatientHub> hubContext, 
            string title, string message, string level = "Info")
        {
            await hubContext.Clients.All.SendAsync("SystemNotification", new
            {
                Title = title,
                Message = message,
                Level = level,
                Timestamp = DateTime.UtcNow
            });
        }

        // Get connected clients count (for monitoring)
        public static int GetConnectedClientsCount(this IHubContext<PatientHub> hubContext)
        {
            // This would need a different approach to get real-time count
            // For now, we're using the static counter in the hub
            return PatientHub.GetStaticConnectionCount();
        }
    }

    // Static method to access connection count from outside the hub
    public partial class PatientHub
    {
        public static int GetStaticConnectionCount() => _connectedClients;
        
        public static int GetPatientSubscriberCount(string patientId)
        {
            return _patientSubscriptions.ContainsKey(patientId) ? _patientSubscriptions[patientId].Count : 0;
        }
        
        public static Dictionary<string, int> GetAllPatientSubscriptions()
        {
            return _patientSubscriptions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }
    }
}