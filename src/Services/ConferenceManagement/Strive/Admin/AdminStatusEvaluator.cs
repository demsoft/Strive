using System;
using System.Collections.Generic;
using System.Linq;

namespace Strive.Admin
{
    /// <summary>
    ///     Decides from the numbers whether the server is fine, nearly full or full, and what to tell the administrator.
    ///     Pure functions, so that the rules can be tested.
    /// </summary>
    public static class AdminStatusEvaluator
    {
        /// <summary>Services whose failure stops conferences. Others (recorder) only degrade.</summary>
        private static readonly string[] EssentialServices = {"sfu", "mongodb", "rabbitmq", "identity", "redis", "api"};

        public static (string Status, IReadOnlyList<AdminAlert> Alerts, CapacityInfo Capacity) Evaluate(
            SfuStats? sfu, bool sfuConfigured, IReadOnlyList<ServiceStatus> services, RecorderStats? recorder,
            int participants, AdminThresholds thresholds)
        {
            var alerts = new List<AdminAlert>();
            var capacityLevel = AdminLevel.Ok;

            void Add(string level, string message, bool capacity = false)
            {
                alerts.Add(new AdminAlert(level, message));
                if (capacity && level == AdminLevel.Critical) capacityLevel = "full";
                else if (capacity && level == AdminLevel.Warning && capacityLevel == AdminLevel.Ok)
                    capacityLevel = AdminLevel.Warning;
            }

            var workers = sfu?.Workers ?? new List<SfuWorker>();
            var maxCpu = workers.Count == 0 ? 0 : workers.Max(x => x.CpuPercent);
            var avgCpu = workers.Count == 0 ? 0 : workers.Average(x => x.CpuPercent);
            double? memoryUsed = null;
            double? loadPerCore = null;

            if (sfu != null)
            {
                foreach (var worker in workers)
                    if (worker.CpuPercent >= thresholds.WorkerCpuCriticalPercent)
                        Add(AdminLevel.Critical,
                            $"Media worker {worker.Index + 1} is at {worker.CpuPercent:0}% of its core: the server is full.",
                            true);
                    else if (worker.CpuPercent >= thresholds.WorkerCpuWarningPercent)
                        Add(AdminLevel.Warning,
                            $"Media worker {worker.Index + 1} is at {worker.CpuPercent:0}% of its core: the server is getting full.",
                            true);

                if (sfu.System.MemoryTotalBytes > 0)
                {
                    memoryUsed = 100d * (sfu.System.MemoryTotalBytes - sfu.System.MemoryAvailableBytes) /
                                 sfu.System.MemoryTotalBytes;
                    if (memoryUsed >= thresholds.MemoryCriticalPercent)
                        Add(AdminLevel.Critical, $"Memory is {memoryUsed:0}% used: the server is out of memory.", true);
                    else if (memoryUsed >= thresholds.MemoryWarningPercent)
                        Add(AdminLevel.Warning, $"Memory is {memoryUsed:0}% used.", true);
                }

                if (sfu.System.CpuCores > 0)
                {
                    loadPerCore = sfu.System.LoadAverage1m / sfu.System.CpuCores;
                    if (loadPerCore >= thresholds.LoadPerCoreCritical)
                        Add(AdminLevel.Critical,
                            $"The processors are overloaded (load {sfu.System.LoadAverage1m:0.0} on {sfu.System.CpuCores} cores).",
                            true);
                    else if (loadPerCore >= thresholds.LoadPerCoreWarning)
                        Add(AdminLevel.Warning,
                            $"The processors are busy (load {sfu.System.LoadAverage1m:0.0} on {sfu.System.CpuCores} cores).",
                            true);
                }
            }
            else if (sfuConfigured)
            {
                Add(AdminLevel.Critical, "The media server does not answer: nobody can join a call.");
            }

            foreach (var service in services.Where(x => x.Status != "up"))
            {
                var essential = EssentialServices.Contains(service.Name.ToLowerInvariant());
                var what = service.Status == "down" ? "is down" : "has a problem";
                Add(essential && service.Status == "down" ? AdminLevel.Critical : AdminLevel.Warning,
                    $"{service.Name} {what}" + (string.IsNullOrEmpty(service.Detail) ? "." : $": {service.Detail}"));
            }

            if (recorder != null)
            {
                if (recorder.MaxConcurrent is { } max && recorder.Active >= max)
                    Add(AdminLevel.Warning, $"The recorder is busy with {recorder.Active} of {max} recordings.");

                if (recorder.Disk is {TotalBytes: > 0} disk)
                {
                    var free = 100d * disk.FreeBytes / disk.TotalBytes;
                    if (free <= thresholds.DiskFreeCriticalPercent)
                        Add(AdminLevel.Critical, $"The disk of the recorder is almost full ({free:0}% free).");
                    else if (free <= thresholds.DiskFreeWarningPercent)
                        Add(AdminLevel.Warning, $"The disk of the recorder is filling up ({free:0}% free).");
                }
            }

            var status = alerts.Any(x => x.Level == AdminLevel.Critical) ? AdminLevel.Critical
                : alerts.Any(x => x.Level == AdminLevel.Warning) ? AdminLevel.Warning
                : AdminLevel.Ok;

            var summary = capacityLevel switch
            {
                "full" => "The server is full or out of resources. New people may not be able to join.",
                AdminLevel.Warning => "The server is getting full.",
                _ => sfu == null ? "No numbers from the media server." : "There is room for more people.",
            };

            var capacity = new CapacityInfo(capacityLevel, summary, workers.Count, Math.Round(maxCpu, 1),
                Math.Round(avgCpu, 1), memoryUsed == null ? null : Math.Round(memoryUsed.Value, 1),
                loadPerCore == null ? null : Math.Round(loadPerCore.Value, 2), participants);

            return (status, alerts, capacity);
        }
    }
}
