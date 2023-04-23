using System.Text.Json.Serialization;

namespace kaspersky_internship_csharp_2023.dto;

public class TaskInfo
{
    public string Id { get; set; }
    public string Status { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LogReport>? Result { get; set; }
}

public class TasksInfo
{
    public List<TaskInfo> Tasks { get; set; }
    public int NumberOfCompletedTasks { get; set; }
    public int NumberOfFaultedTasks { get; set; }
    public int NumberOfInProgressTasks { get; set; }
    public int NumberOfTasks { get; set; }
}

public class CreateTaskResult
{
    public string Id { get; set; }
}

public class LogReport
{
    public string ServiceName { get; set; }
    public DateTime? EarliestEntry { get; set; }
    public DateTime? LatestEntry { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; }
    public int NumberOfRotations { get; set; }
}

public class CreateTaskRequest
{
    public string LogDirectory { get; set; }
    public string ServiceNameRegex { get; set; }
}