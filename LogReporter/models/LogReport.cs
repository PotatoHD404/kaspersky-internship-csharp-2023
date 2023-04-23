namespace kaspersky_internship_csharp_2023.models;

public class ReportTask
{
    public string Id { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsFaulted { get; set; }
    public string LogDirectory { get; set; }
    public string ServiceNameRegex { get; set; }
    public List<LogReport> LogReports { get; set; } = new();
}

public class LogReport
{
    public int Id { get; set; }
    public string ServiceName { get; set; }
    public DateTime? EarliestEntry { get; set; }
    public DateTime? LatestEntry { get; set; }
    public int NumberOfRotations { get; set; }
    public List<CategoryCount> CategoryCounts { get; set; } = new();
    public ReportTask ReportTask = null!;
}

public class CategoryCount
{
    public int Id { get; set; }
    public LogReport LogReport = null!;
    public string Category { get; set; }
    public int Count { get; set; }
}