using System;
using System.Collections.Generic;

namespace Strive.Admin
{
    public static class AdminLevel
    {
        public const string Ok = "ok";
        public const string Warning = "warning";
        public const string Critical = "critical";
    }

    public record AdminAlert(string Level, string Message);

    /// <param name="Level">ok, warning, or full (no room for more people)</param>
    public record CapacityInfo(string Level, string Summary, int WorkerCount, double MaxWorkerCpuPercent,
        double AverageWorkerCpuPercent, double? MemoryUsedPercent, double? LoadPerCore, int Participants);

    public record ConferenceSummary(string ConferenceId, string? Name, int Participants, DateTimeOffset? OpenedAt,
        int? Connections, int? Producers, int? Consumers);

    public record ConferencesInfo(int Open, int Participants, IReadOnlyList<ConferenceSummary> List);

    public record ServiceStatus(string Name, string Status, string? Detail, int? LatencyMs);

    public record AdminOverview(DateTimeOffset GeneratedAt, string Status, IReadOnlyList<AdminAlert> Alerts,
        CapacityInfo Capacity, ConferencesInfo Conferences, SfuStats? Sfu, IReadOnlyList<ServiceStatus> Services,
        AccountStats? Accounts, RecorderStats? Recorder);

    /// <summary>What the media server reports</summary>
    public class SfuStats
    {
        public long UptimeSeconds { get; set; }
        public List<SfuWorker> Workers { get; set; } = new();
        public List<SfuConference> Conferences { get; set; } = new();
        public SfuTotals Totals { get; set; } = new();
        public SfuSystem System { get; set; } = new();
    }

    public class SfuWorker
    {
        public int Index { get; set; }
        public int Pid { get; set; }
        public double CpuPercent { get; set; }
        public long MemoryKb { get; set; }
    }

    public class SfuConference
    {
        public string ConferenceId { get; set; } = string.Empty;
        public int Participants { get; set; }
        public int Connections { get; set; }
        public int Producers { get; set; }
        public int Consumers { get; set; }
    }

    public class SfuTotals
    {
        public int Conferences { get; set; }
        public int Participants { get; set; }
        public int Connections { get; set; }
        public int Producers { get; set; }
        public int Consumers { get; set; }
    }

    public class SfuSystem
    {
        public int CpuCores { get; set; }
        public double LoadAverage1m { get; set; }
        public long MemoryTotalBytes { get; set; }
        public long MemoryAvailableBytes { get; set; }
        public long ProcessMemoryBytes { get; set; }
    }

    public class AccountStats
    {
        public bool Available { get; set; }
        public int TotalUsers { get; set; }
        public int ConfirmedUsers { get; set; }
        public int WithPassword { get; set; }
        public int WithGoogle { get; set; }
        public List<SignupsPerDay> Signups { get; set; } = new();
    }

    public class SignupsPerDay
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class RecorderStats
    {
        public int Active { get; set; }
        public int? MaxConcurrent { get; set; }
        public DiskInfo? Disk { get; set; }
    }

    public class DiskInfo
    {
        public double FreeBytes { get; set; }
        public double TotalBytes { get; set; }
    }

    /// <summary>One point of the history (one per minute)</summary>
    public class AdminSample
    {
        [MongoDB.Bson.Serialization.Attributes.BsonId]
        public MongoDB.Bson.ObjectId Id { get; set; }

        public DateTime T { get; set; }
        public int Participants { get; set; }
        public int Conferences { get; set; }
        public double MaxWorkerCpu { get; set; }
        public double AverageWorkerCpu { get; set; }
        public double? MemoryUsedPercent { get; set; }
        public double? LoadPerCore { get; set; }
        public int Alerts { get; set; }
    }
}
