using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using kaspersky_internship_csharp_2023.data;
using kaspersky_internship_csharp_2023.dto;
using kaspersky_internship_csharp_2023.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogReport = kaspersky_internship_csharp_2023.dto.LogReport;

namespace kaspersky_internship_csharp_2023.Controllers;

[ApiController]
[Route("LogReports")]
public partial class LogReportController : ControllerBase
{
    private static ConcurrentDictionary<String, Task<Task<List<LogReport>?>>> tasks = new ();
    private readonly LogReportContext _context;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    
    public LogReportController(LogReportContext context, IServiceScopeFactory serviceScopeFactory)
    {
        _context = context;
        _serviceScopeFactory = serviceScopeFactory;
        _context.Database.EnsureCreated();
    }
    
    [HttpPost]
    public ActionResult<CreateTaskResult> Post([FromBody] CreateTaskRequest request)
    {
        var logDirectory = request.LogDirectory;
        var serviceNameRegex = request.ServiceNameRegex;


        if (!Directory.Exists(logDirectory))
        {
            return BadRequest();
        }
        var guid = Guid.NewGuid().ToString();
        _context.ReportTasks.Add(new ReportTask
        {
            Id = guid,
            IsCompleted = false,
            IsFaulted = false,
            LogDirectory = logDirectory,
            ServiceNameRegex = serviceNameRegex
        });
        _context.SaveChangesAsync();
        var task = GenerateReportsAsync(logDirectory, serviceNameRegex).ContinueWith(async (t) =>
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _context = scope.ServiceProvider.GetRequiredService<LogReportContext>();

                

                var reportTask = await _context.ReportTasks.FindAsync(guid);

                if (reportTask == null)
                {
                    Console.WriteLine("ReportTask not found");
                    return null;
                }
                reportTask.IsCompleted = t.IsCompleted;
                reportTask.IsFaulted = t.IsFaulted;
                if (t.IsFaulted)
                {
                    Console.WriteLine("Task is faulted");
                    return null;
                }

                reportTask.LogReports = t.Result.Select(logReport => new models.LogReport
                {
                    ServiceName = logReport.ServiceName,
                    EarliestEntry = logReport.EarliestEntry,
                    LatestEntry = logReport.LatestEntry,
                    NumberOfRotations = logReport.NumberOfRotations,
                    CategoryCounts = logReport.CategoryCounts.Select(categoryCount => new CategoryCount
                    {
                        Category = categoryCount.Key,
                        Count = categoryCount.Value
                    }).ToList()
                }).ToList();

                try
                {
                   await _context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                tasks.TryRemove(guid, out _);

            }
            return t.Result;
        });


        tasks.TryAdd(guid, task);

        return new CreateTaskResult {Id = guid};
    }
    
    [HttpGet]
    [Route("{id}")]
    public ActionResult<TaskInfo> GetOne(string id)
    {
        var reportTask = _context.ReportTasks.Include(rt => rt.LogReports)
            .ThenInclude(lr => lr.CategoryCounts)
            .SingleOrDefault(rt => rt.Id == id);
        if (reportTask == null)
        {
            return NotFound();
        }
        
        if (reportTask.IsCompleted)
        {
            return new TaskInfo {Id = id, Status = "Completed", Result = reportTask.LogReports.Select(logReport => new LogReport
            {
                ServiceName = logReport.ServiceName,
                EarliestEntry = logReport.EarliestEntry,
                LatestEntry = logReport.LatestEntry,
                NumberOfRotations = logReport.NumberOfRotations,
                CategoryCounts = logReport.CategoryCounts.ToDictionary(categoryCount => categoryCount.Category, categoryCount => categoryCount.Count)
            }).ToList()};
        }
        
        if (reportTask.IsFaulted)
        {
            return new TaskInfo {Id = id, Status = "Faulted"};
        }
        
        return new TaskInfo {Id = id, Status = "In progress"};
    }
    
    [HttpGet]
    public ActionResult<TasksInfo> GetAll()
    {
        var tasksInfo = new TasksInfo();
        tasksInfo.Tasks = _context.ReportTasks.Select(reportTask => new TaskInfo
        {
            Id = reportTask.Id,
            Status = reportTask.IsCompleted ? "Completed" : reportTask.IsFaulted ? "Faulted" : "In progress"
        }).ToList();
        tasksInfo.NumberOfTasks = tasksInfo.Tasks.Count;
        tasksInfo.NumberOfCompletedTasks = tasksInfo.Tasks.Count(task => task.Status == "Completed");
        tasksInfo.NumberOfFaultedTasks = tasksInfo.Tasks.Count(task => task.Status == "Faulted");
        tasksInfo.NumberOfInProgressTasks = tasksInfo.Tasks.Count(task => task.Status == "In progress");
        return tasksInfo;
    }

    private async Task<List<LogReport>> GenerateReportsAsync(string logDirectory, string serviceNameRegex)
    {
        var regex = new Regex(serviceNameRegex);

        var serviceLogFiles = Directory.GetFiles(logDirectory)
            .Where(file => regex.IsMatch(Path.GetFileNameWithoutExtension(file).Split('.')[0])).ToList();
        var curTasks = serviceLogFiles.Select(async serviceLogFile => await ProcessLogFileAsync(serviceLogFile)).ToArray();
        await Task.WhenAll(curTasks);
        var keyValuePairs = curTasks.Select(task => new KeyValuePair<string,  LogReport>(task.Result.ServiceName, task.Result));
        var reports = keyValuePairs.GroupBy(pair => pair.Key)
            .ToDictionary(group => group.Key, group => new LogReport
            {
                ServiceName = group.Key,
                CategoryCounts = group.Select(pair => pair.Value.CategoryCounts)
                    .Aggregate((dict1, dict2) => dict1.Concat(dict2)
                        .GroupBy(pair => pair.Key)
                        .ToDictionary(g => g.Key, g => g.Sum(pair => pair.Value))),
                NumberOfRotations = group.Sum(pair => pair.Value.NumberOfRotations),
                EarliestEntry = group.Select(pair => pair.Value.EarliestEntry).Min(),
                LatestEntry = group.Select(pair => pair.Value.LatestEntry).Max()
            }).Values.ToList();

        return reports;
    }
    
    static string MaskEverySecondCharacter(string input)
    {
        char[] maskedArray = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            maskedArray[i] = i % 2 == 0 ? input[i] : '*';
        }
        return new string(maskedArray);
    }

    private async Task<LogReport> ProcessLogFileAsync(string logFile)
    {
        var logReport = new LogReport();

        var fileName = Path.GetFileName(logFile);
        if (fileName == null)
        {
            throw new Exception("File name is null");
        }
        if (fileName.Split('.').Length < 2)
        {
            throw new Exception("File name is invalid");
        }
        var serviceName = fileName.Split('.')[0];
        logReport.ServiceName = serviceName;

        var logLines = await System.IO.File.ReadAllLinesAsync(logFile);

        DateTime minDate = DateTime.MaxValue;
        DateTime maxDate = DateTime.MinValue;
        var categoryCounts = new Dictionary<string, int>();
        var rotationRegex = FileRegex();
        var emailRegex = EmailRegex();
        foreach (var logLine in logLines)
        {
            var line = logLine.Trim();
            // replace every second symbol in emails with *
            var emails = emailRegex.Matches(line);
            foreach (Match email in emails)
            {
                string originalEmail = email.Value;
                string maskedName = MaskEverySecondCharacter(email.Groups["name"].Value);
                string domain = email.Groups["domain"].Value;
                string maskedEmail = maskedName + "@" + domain;

                line = line.Replace(originalEmail, maskedEmail);
            }
            if (!DateTime.TryParse(line.Substring(1, 19), out DateTime logDate))
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

            var category = line.Split(']')[1].Trim().Substring(1);
            if (!categoryCounts.ContainsKey(category))
            {
                categoryCounts[category] = 0;
            }

            categoryCounts[category]++;
        }

        logReport.EarliestEntry = minDate == DateTime.MaxValue ? null : minDate;
        logReport.LatestEntry = maxDate == DateTime.MinValue ? null : maxDate;
        logReport.CategoryCounts = categoryCounts;
        var directoryName = Path.GetDirectoryName(logFile);
        if (directoryName == null)
        {
            return logReport;
        }
        var rotationFiles = Directory.GetFiles(directoryName, $"{serviceName}.*.log");
        logReport.NumberOfRotations = rotationFiles.Count(file => rotationRegex.IsMatch(file));

        return logReport;
    }
    
    

    [GeneratedRegex("\\d+\\.log$")]
    private static partial Regex FileRegex();
    // email regex named group
    [GeneratedRegex("(?<name>\\w+)@(?<domain>\\w+\\.\\w{2,})")]
    private static partial Regex EmailRegex();
    
    
}