using System.Text.Json; 
using System.Text.Json.Serialization;

namespace HealthFlow.Shared.Models;
// Patient DTO matching your JSON structure
public class PatientJson
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("firstName")] 
    public string FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string LastName { get; set; }

    [JsonPropertyName("medicalRecordNumber")]
    public string MedicalRecordNumber { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public DateTime DateOfBirth { get; set; }

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}