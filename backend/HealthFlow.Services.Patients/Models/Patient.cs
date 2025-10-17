
namespace HealthFlow.Services.Patients.Models;

public class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string MedicalRecordNumber { get; set; } = string.Empty;
    public PatientStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Computed property for full name
    public string FullName => $"{FirstName} {LastName}";

    // Computed property for age
    public int Age
    {
        get
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}

public enum PatientStatus
{
    Admitted,
    Discharged,
    InTreatment,
    Critical
}