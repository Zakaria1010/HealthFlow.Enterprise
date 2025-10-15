

namespace HealthFlow.Services.Analytics.Models;

public class DashboardData
{
    public int TotalPatients { get; set; }
    public double AverageWaitTime { get; set; }
    public int CriticalPatients { get; set; }
    public int AdmittedToday { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<string, int> PatientStatusDistribution { get; set; } = new();
    public List<HourlyAdmission> HourlyAdmissions { get; set; } = new();
    public List<WaitTimeTrend> WaitTimeTrend { get; set; } = new();
    public SystemHealth SystemHealth { get; set; } = new();
}

public class HourlyAdmission
{
    public string Hour { get; set; } = string.Empty;
    public int Admissions { get; set; }
}

public class WaitTimeTrend
{
    public string Date { get; set; } = string.Empty;
    public double WaitTime { get; set; }
}

public class SystemHealth
{
    public string PatientService { get; set; } = string.Empty;
    public string AnalyticsService { get; set; } = string.Empty;
    public string BackgroundWorker { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string MessageQueue { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
}

public class BroadcastMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info"; // Info, Warning, Error
}