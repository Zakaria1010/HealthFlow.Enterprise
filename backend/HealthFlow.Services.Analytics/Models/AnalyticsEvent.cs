using System.Text.Json.Serialization;

namespace HealthFlow.Shared.Models
{
    public class AnalyticsEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("patientId")]
        public string PatientId { get; set; } = string.Empty;

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("payload")]
        public object Payload { get; set; } = new();

        [JsonPropertyName("service")]
        public string Service { get; set; } = string.Empty;

        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        // Cosmos DB required property
        [JsonPropertyName("partitionKey")]
        public string PartitionKey => PatientId;

        // Helper methods for common event types
        public static AnalyticsEvent CreatePatientEvent(string patientId, string eventType, object payload)
        {
            return new AnalyticsEvent
            {
                PatientId = patientId,
                EventType = eventType,
                Payload = payload,
                Service = "PatientService",
                Timestamp = DateTime.UtcNow
            };
        }

        public static AnalyticsEvent CreateDeviceEvent(string patientId, string eventType, object payload)
        {
            return new AnalyticsEvent
            {
                PatientId = patientId,
                EventType = eventType,
                Payload = payload,
                Service = "DeviceService",
                Timestamp = DateTime.UtcNow
            };
        }

        public static AnalyticsEvent CreateSystemEvent(string eventType, object payload)
        {
            return new AnalyticsEvent
            {
                PatientId = "system",
                EventType = eventType,
                Payload = payload,
                Service = "System",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    // Predefined event types for consistency
    public static class EventTypes
    {
        // Patient events
        public const string PatientCreated = "PatientCreated";
        public const string PatientUpdated = "PatientUpdated";
        public const string PatientStatusChanged = "PatientStatusChanged";
        public const string PatientAdmitted = "PatientAdmitted";
        public const string PatientDischarged = "PatientDischarged";
        public const string PatientCritical = "PatientCritical";

        // Vital signs events
        public const string VitalSignsUpdated = "VitalSignsUpdated";
        public const string BloodPressureMeasured = "BloodPressureMeasured";
        public const string HeartRateMeasured = "HeartRateMeasured";
        public const string TemperatureMeasured = "TemperatureMeasured";

        // Device events
        public const string DeviceConnected = "DeviceConnected";
        public const string DeviceDisconnected = "DeviceDisconnected";
        public const string DeviceAlert = "DeviceAlert";

        // System events
        public const string SystemAlert = "SystemAlert";
        public const string PerformanceMetric = "PerformanceMetric";
        public const string ErrorOccurred = "ErrorOccurred";
    }

    // Common payload structures
    public class PatientEventPayload
    {
        public string PatientId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PreviousStatus { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class VitalSignsPayload
    {
        public string PatientId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public double? HeartRate { get; set; }
        public double? BloodPressureSystolic { get; set; }
        public double? BloodPressureDiastolic { get; set; }
        public double? Temperature { get; set; }
        public double? OxygenSaturation { get; set; }
        public string? Status { get; set; } // Normal, Warning, Critical
        public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    }

    public class SystemAlertPayload
    {
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // Info, Warning, Error, Critical
        public string Service { get; set; } = string.Empty;
        public object? AdditionalData { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    }
}