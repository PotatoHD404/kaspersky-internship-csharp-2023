using kaspersky_internship_csharp_2023.models;
using Microsoft.EntityFrameworkCore;

namespace kaspersky_internship_csharp_2023.data;

public class LogReportContext : DbContext
{
    public LogReportContext(DbContextOptions<LogReportContext> options) : base(options)
    {
    }

    public DbSet<ReportTask> ReportTasks { get; set; }
    public DbSet<LogReport> LogReports { get; set; }
    public DbSet<CategoryCount> CategoryCounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // modelBuilder.Entity<ReportTask>().ToTable("report_tasks");
        // modelBuilder.Entity<LogReport>().ToTable("log_reports");
        // modelBuilder.Entity<CategoryCount>().ToTable("category_counts");
    }
}