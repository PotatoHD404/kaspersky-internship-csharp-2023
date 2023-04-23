using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using kaspersky_internship_csharp_2023.dto;
using Microsoft.AspNetCore.Mvc;

namespace kaspersky_internship_csharp_2023.Controllers;

[ApiController]
[Route("[controller]")]
public partial class LogReportController : ControllerBase
{
    private Dictionary<String, Task<List<LogReport>>> tasks = new Dictionary<string, Task<List<LogReport>>>();
    
    
    [HttpGet]
    [Route("AddTask")]
    public ActionResult<LogInfo> SetupTak(string logDirectory, string serviceNameRegex)
    {
        var guid = Guid.NewGuid().ToString();
        var task = GenerateReportsAsync(logDirectory, serviceNameRegex);
        tasks[guid] = task;
        task.Start();
        
        return new LogInfo {Id = guid};
    }
    
    [HttpGet]
    [Route("GetTask")]
    public ActionResult<TaskInfo> GetTask(string id)
    {
        if (!tasks.ContainsKey(id))
        {
            return NotFound();
        }

        var task = tasks[id];
        if (task.IsCompleted)
        {
            return new TaskInfo {Id = id, Status = "Completed", Result = task.Result};
        }

        if (task.IsFaulted)
        {
            return new TaskInfo {Id = id, Status = "Faulted"};
        }

        return new TaskInfo {Id = id, Status = "In progress"};
    }


    private async Task<List<LogReport>> GenerateReportsAsync(string logDirectory, string serviceNameRegex)
    {
        var reports = new List<LogReport>();

        var regex = new Regex(serviceNameRegex);
        var serviceLogFiles = Directory.GetFiles(logDirectory)
            .Where(file => regex.IsMatch(Path.GetFileNameWithoutExtension(file).Split('.')[0]));

        foreach (var serviceLogFile in serviceLogFiles)
        {
            var report = await ProcessLogFileAsync(serviceLogFile);
            if (report != null)
            {
                reports.Add(report);
            }
        }

        return reports;
    }

    private async Task<LogReport?> ProcessLogFileAsync(string logFile)
    {
        var logReport = new LogReport();

        var fileName = Path.GetFileName(logFile);
        var serviceName = fileName.Split('.')[0];
        logReport.ServiceName = serviceName;

        var logLines = await System.IO.File.ReadAllLinesAsync(logFile);

        DateTime minDate = DateTime.MaxValue;
        DateTime maxDate = DateTime.MinValue;
        var categoryCounts = new Dictionary<string, int>();
        var rotationRegex = MyRegex();

        foreach (var logLine in logLines)
        {
            if (!DateTime.TryParse(logLine.Substring(1, 19), out DateTime logDate))
            {
                continue;
            }

            if (logDate < minDate)
            {
                minDate = logDate;
            }

            if (logDate > maxDate)
            {
                maxDate = logDate;
            }

            var category = logLine.Split(']')[1].Trim();
            if (!categoryCounts.ContainsKey(category))
            {
                categoryCounts[category] = 0;
            }

            categoryCounts[category]++;
        }

        logReport.EarliestEntry = minDate;
        logReport.LatestEntry = maxDate;
        logReport.CategoryCounts = categoryCounts;
        var directoryName = Path.GetDirectoryName(logFile);
        if (directoryName == null)
        {
            return null;
        }
        var rotationFiles = Directory.GetFiles(directoryName, $"{serviceName}.*.log");
        logReport.NumberOfRotations = rotationFiles.Count(file => rotationRegex.IsMatch(file));

        return logReport;
    }

    [GeneratedRegex("\\d+\\.log$")]
    private static partial Regex MyRegex();
}


/*[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }
}*/