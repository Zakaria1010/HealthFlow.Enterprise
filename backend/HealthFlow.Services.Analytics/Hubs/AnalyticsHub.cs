using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HealthFlow.Services.Analytics.Hubs
{
    public class AnalyticsHub : Hub
    {
        private readonly ILogger<AnalyticsHub> _logger;
        private static int _connectedClients = 0;

        public AnalyticsHub(ILogger<AnalyticsHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            Interlocked.Increment(ref _connectedClients);
            _logger.LogInformation("Client connected to AnalyticsHub: {ConnectionId}. Total clients: {ConnectedClients}", 
                Context.ConnectionId, _connectedClients);
            
            // Send current system status to newly connected client
            await Clients.Caller.SendAsync("ConnectionEstablished", new { 
                message = "Connected to HealthFlow Analytics Hub",
                connectionId = Context.ConnectionId,
                serverTime = DateTime.UtcNow
            });
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Interlocked.Decrement(ref _connectedClients);
            _logger.LogInformation("Client disconnected from AnalyticsHub: {ConnectionId}. Total clients: {ConnectedClients}", 
                Context.ConnectionId, _connectedClients);
            
            await base.OnDisconnectedAsync(exception);
        }

        // Client can call this method to request current dashboard data
        public async Task RequestDashboardData()
        {
            _logger.LogInformation("Client {ConnectionId} requested dashboard data", Context.ConnectionId);
            // In a real implementation, you might fetch and send current data
            await Clients.Caller.SendAsync("DashboardDataRequested", new { 
                timestamp = DateTime.UtcNow,
                message = "Use REST API for full dashboard data"
            });
        }

        // Client can subscribe to specific patient events
        public async Task SubscribeToPatient(string patientId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{patientId}");
            _logger.LogInformation("Client {ConnectionId} subscribed to patient {PatientId}", 
                Context.ConnectionId, patientId);
            
            await Clients.Caller.SendAsync("SubscribedToPatient", new { 
                patientId = patientId,
                message = $"Subscribed to updates for patient {patientId}"
            });
        }

        // Client can unsubscribe from patient events
        public async Task UnsubscribeFromPatient(string patientId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"patient-{patientId}");
            _logger.LogInformation("Client {ConnectionId} unsubscribed from patient {PatientId}", 
                Context.ConnectionId, patientId);
        }

        // Method to broadcast to all connected clients
        public async Task BroadcastMessage(string message)
        {
            _logger.LogInformation("Broadcasting message to all clients: {Message}", message);
            await Clients.All.SendAsync("BroadcastMessage", new {
                message = message,
                timestamp = DateTime.UtcNow,
                from = "System"
            });
        }
    }
}