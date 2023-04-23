using System;
using System.Collections.Concurrent;
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
[Route("LogReports")]
public partial class LogReportController : ControllerBase
{
    private static ConcurrentDictionary<String, Task<List<LogReport>>> tasks = new ConcurrentDictionary<string, Task<List<LogReport>>>();
    
    
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
        var task = GenerateReportsAsync(logDirectory, serviceNameRegex);
        tasks.TryAdd(guid, task);

        return new CreateTaskResult {Id = guid};
    }
    
    [HttpGet]
    [Route("{id}")]
    public ActionResult<TaskInfo> GetOne(string id)
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
    
    [HttpGet]
    public ActionResult<TasksInfo> GetAll()
    {
        var tasksInfo = new TasksInfo();
        tasksInfo.Tasks = new List<TaskInfo>();
        
        foreach (var (id, task) in tasks.ToArray())
        {

            if (task.IsCompleted)
            {
                tasksInfo.Tasks.Add(new TaskInfo {Id = id, Status = "Completed"});
                tasksInfo.NumberOfCompletedTasks++;
            }
            else if (task.IsFaulted)
            {
                tasksInfo.Tasks.Add(new TaskInfo {Id = id, Status = "Faulted"});
                tasksInfo.NumberOfFaultedTasks++;
            }
            else
            {
                tasksInfo.Tasks.Add(new TaskInfo {Id = id, Status = "In progress"});
                tasksInfo.NumberOfInProgressTasks++;
            }
            tasksInfo.NumberOfTasks++;
        }

        return tasksInfo;
    }
    
    [HttpDelete]
    [Route("{id}")]
    public ActionResult Delete(string id)
    {
        if (!tasks.ContainsKey(id))
        {
            return NotFound();
        }

        tasks.Remove(id, out _);
        return Ok();
    }


    private async Task<List<LogReport>> GenerateReportsAsync(string logDirectory, string serviceNameRegex)
    {
        var regex = new Regex(serviceNameRegex);

        var serviceLogFiles = Directory.GetFiles(logDirectory)
            .Where(file => regex.IsMatch(Path.GetFileNameWithoutExtension(file).Split('.')[0])).ToList();
        var curTasks = serviceLogFiles.Select(async serviceLogFile => await ProcessLogFileAsync(serviceLogFile)).ToArray();
        await Task.WhenAll(curTasks);
        var keyValuePairs = curTasks.Select(task => new KeyValuePair<string,  LogReport>(task.Result.ServiceName, task.Result));
        Dictionary<string, LogReport> reports = keyValuePairs.GroupBy(pair => pair.Key)
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
            });


        return reports.Values.ToList();
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