using System.Text.Json.Serialization;

namespace HealthFlow.Shared.Models;


public record PatientMessage(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("patientId")] string PatientId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("payload")] object Payload,
    [property: JsonPropertyName("correlationId")] string CorrelationId = null,
    [property: JsonPropertyName("version")] string Version = "1.0") 
{
    public PatientMessage(): this(
        Guid.NewGuid(),
        string.Empty,
        string.Empty,
        DateTime.UtcNow,
        new object())
    {
    }

    // Factory methods
    public static PatientMessage CreatePatientCreated(string patientId, PatientCreatedPayload patientData, string correlationId = null)
    {
        return new PatientMessage(
            Guid.NewGuid(),
            patientId,
            EventTypes.PatientCreated,
            DateTime.UtcNow,
            patientData, // Make sure this is a serializable object
            correlationId ?? Guid.NewGuid().ToString());
    }

    public static PatientMessage CreatePatientUpdated(string patientId, PatientUpdatedPayload patientData, string correlationId = null)
    {
        return new PatientMessage(
            Guid.NewGuid(),
            patientId,
            EventTypes.PatientUpdated,
            DateTime.UtcNow,
            patientData,
            correlationId ?? Guid.NewGuid().ToString());
    }

    public static PatientMessage CreatePatientStatusChanged(string patientId, string oldStatus, string newStatus, string correlationId = null)
    {
        return new PatientMessage(
            Guid.NewGuid(),
            patientId,
            EventTypes.PatientStatusChanged,
            DateTime.UtcNow,
            new { OldStatus = oldStatus, NewStatus = newStatus },
            correlationId ?? Guid.NewGuid().ToString());
    }

    public static PatientMessage CreatePatientDeleted(string patientId, PatientDeletedPayload patientInfo, string correlationId = null)
    {
        return new PatientMessage(
            Guid.NewGuid(),
            patientId,
            EventTypes.PatientDeleted,
            DateTime.UtcNow,
            patientInfo,
            correlationId ?? Guid.NewGuid().ToString()); 
    }

    public static PatientMessage CreateVitalSignsUpdated(string patientId, VitalSignsPayload vitalSigns, string correlationId = null)
    {
        return new PatientMessage(
            Guid.NewGuid(),
            patientId,
            EventTypes.VitalSignsUpdated,
            DateTime.UtcNow,
            vitalSigns,
            correlationId ?? Guid.NewGuid().ToString());
    }

    public static PatientMessage CreateCriticalAlert(string patientId, CriticalAlertPayload alertData, string correlationId = null)
    {
        return new PatientMessage(
            Guid.NewGuid(),
            patientId,
            EventTypes.PatientCritical,
            DateTime.UtcNow,
            alertData,
            correlationId ?? Guid.NewGuid().ToString());
    }
}

// Specific payload classes for type safety
public record PatientCreatedPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("medicalRecordNumber")] string MedicalRecordNumber,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("dateOfBirth")] DateTime DateOfBirth,
    [property: JsonPropertyName("age")] int Age,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt);

public record PatientUpdatedPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("medicalRecordNumber")] string MedicalRecordNumber,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("previousValues")] object PreviousValues = null);

public record PatientStatusChangedPayload(
    [property: JsonPropertyName("patientId")] string PatientId,
    [property: JsonPropertyName("oldStatus")] string OldStatus,
    [property: JsonPropertyName("newStatus")] string NewStatus,
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("changedAt")] DateTime ChangedAt,
    [property: JsonPropertyName("reason")] string Reason = null);

public record PatientDeletedPayload(
    [property: JsonPropertyName("patientId")] string PatientId,
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("medicalRecordNumber")] string MedicalRecordNumber,
    [property: JsonPropertyName("deletedAt")] DateTime DeletedAt);

public record VitalSignsPayload(
        [property: JsonPropertyName("patientId")] string PatientId,
        [property: JsonPropertyName("deviceId")] string DeviceId = null,
        [property: JsonPropertyName("heartRate")] int? HeartRate = null,
        [property: JsonPropertyName("bloodPressureSystolic")] int? BloodPressureSystolic = null,
        [property: JsonPropertyName("bloodPressureDiastolic")] int? BloodPressureDiastolic = null,
        [property: JsonPropertyName("temperature")] double? Temperature = null,
        [property: JsonPropertyName("oxygenSaturation")] double? OxygenSaturation = null,
        [property: JsonPropertyName("respiratoryRate")] int? RespiratoryRate = null,
        [property: JsonPropertyName("measuredAt")] DateTime MeasuredAt = default,
        [property: JsonPropertyName("status")] string Status = "Normal");


public record CriticalAlertPayload(
        [property: JsonPropertyName("patientId")] string PatientId,
        [property: JsonPropertyName("fullName")] string FullName,
        [property: JsonPropertyName("alertType")] string AlertType,
        [property: JsonPropertyName("severity")] string Severity,
        [property: JsonPropertyName("vitalSigns")] object VitalSigns = null,
        [property: JsonPropertyName("message")] string Message = null,
        [property: JsonPropertyName("triggeredAt")] DateTime TriggeredAt = default);