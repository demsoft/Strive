using System.Collections.Generic;
using System.Linq;
using Strive.Admin;
using Xunit;

namespace Strive.Tests.Admin
{
    public class AdminStatusEvaluatorTests
    {
        private static readonly AdminThresholds T = new();
        private static readonly IReadOnlyList<ServiceStatus> NoServices = new List<ServiceStatus>();

        private static SfuStats Sfu(double[] workerCpu, double memoryUsedPercent = 25, int cores = 8, double load = 1) => new()
        {
            Workers = workerCpu.Select((cpu, i) => new SfuWorker {Index = i, Pid = 100 + i, CpuPercent = cpu}).ToList(),
            System = new SfuSystem
            {
                CpuCores = cores, LoadAverage1m = load, MemoryTotalBytes = 100_000_000_000,
                MemoryAvailableBytes = (long) ((100 - memoryUsedPercent) * 1_000_000_000),
            },
        };

        private static (string Status, IReadOnlyList<AdminAlert> Alerts, CapacityInfo Capacity) Run(SfuStats? sfu,
            bool configured = true, IReadOnlyList<ServiceStatus>? services = null, RecorderStats? recorder = null,
            int participants = 0) =>
            AdminStatusEvaluator.Evaluate(sfu, configured, services ?? NoServices, recorder, participants, T);

        [Fact]
        public void QuietServer_IsOk_WithRoomForMore()
        {
            var (status, alerts, capacity) = Run(Sfu(new[] {10d, 20d, 5d, 0d}), participants: 12);

            Assert.Equal("ok", status);
            Assert.Empty(alerts);
            Assert.Equal("ok", capacity.Level);
            Assert.Equal(20, capacity.MaxWorkerCpuPercent);
            Assert.Equal(8.8, capacity.AverageWorkerCpuPercent, 1);
            Assert.Equal(25, capacity.MemoryUsedPercent);
            Assert.Equal(0.125, capacity.LoadPerCore!.Value, 2);
            Assert.Equal(12, capacity.Participants);
            Assert.Equal(4, capacity.WorkerCount);
            Assert.Contains("room", capacity.Summary);
        }

        [Theory]
        [InlineData(69.9, "ok", "ok")]
        [InlineData(70, "warning", "warning")]
        [InlineData(89.9, "warning", "warning")]
        [InlineData(90, "critical", "full")]
        [InlineData(100, "critical", "full")]
        public void WorkerCpu_DecidesWhetherTheServerIsFull(double cpu, string status, string capacityLevel)
        {
            var result = Run(Sfu(new[] {5d, cpu}));

            Assert.Equal(status, result.Status);
            Assert.Equal(capacityLevel, result.Capacity.Level);
        }

        [Fact]
        public void TheBusiestWorkerCounts_NotTheAverage()
        {
            var (status, alerts, capacity) = Run(Sfu(new[] {95d, 0d, 0d, 0d}));

            Assert.Equal("critical", status);
            Assert.Equal("full", capacity.Level);
            Assert.Contains("Media worker 1", Assert.Single(alerts).Message);
            Assert.Equal(23.8, capacity.AverageWorkerCpuPercent, 1);
        }

        [Theory]
        [InlineData(25, "ok", "ok")]
        [InlineData(79.9, "ok", "ok")]
        [InlineData(80, "warning", "warning")]
        [InlineData(91.9, "warning", "warning")]
        [InlineData(92, "critical", "full")]
        public void Memory_UsedDecidesTheLevel(double used, string status, string capacityLevel)
        {
            var result = Run(Sfu(new[] {1d}, used));

            Assert.Equal(status, result.Status);
            Assert.Equal(capacityLevel, result.Capacity.Level);
            Assert.Equal(used, result.Capacity.MemoryUsedPercent!.Value, 1);
        }

        [Fact]
        public void LowMemory_MeansFull()
        {
            var result = Run(Sfu(new[] {1d}, 99));

            Assert.Equal("critical", result.Status);
            Assert.Equal("full", result.Capacity.Level);
            Assert.Contains("out of memory", result.Alerts.Single().Message);
        }

        [Fact]
        public void Overloaded_Processors_AreReported()
        {
            var warning = Run(Sfu(new[] {1d}, cores: 4, load: 3.4));
            var critical = Run(Sfu(new[] {1d}, cores: 4, load: 5));

            Assert.Equal("warning", warning.Status);
            Assert.Equal("critical", critical.Status);
            Assert.Equal("full", critical.Capacity.Level);
        }

        [Fact]
        public void MediaServerThatDoesNotAnswer_IsCritical()
        {
            var (status, alerts, capacity) = Run(null);

            Assert.Equal("critical", status);
            Assert.Contains("media server", alerts.Single().Message);
            Assert.Equal(0, capacity.WorkerCount);
        }

        [Fact]
        public void MediaServerThatIsNotConfigured_IsNotAProblem()
        {
            var (status, alerts, _) = Run(null, false);

            Assert.Equal("ok", status);
            Assert.Empty(alerts);
        }

        [Fact]
        public void EssentialServiceThatIsDown_IsCritical_OptionalOnesOnlyWarn()
        {
            var services = new List<ServiceStatus>
            {
                new("mongodb", "down", "no connection", 5), new("recorder", "down", "timeout", 4000),
                new("rabbitmq", "up", null, 3),
            };

            var (_, alerts, capacity) = Run(Sfu(new[] {1d}), services: services);

            Assert.Equal("critical", alerts.Single(x => x.Message.StartsWith("mongodb")).Level);
            Assert.Equal("warning", alerts.Single(x => x.Message.StartsWith("recorder")).Level);
            Assert.Contains("no connection", alerts.First().Message);
            // a database that is down is not a capacity problem
            Assert.Equal("ok", capacity.Level);
        }

        [Fact]
        public void DegradedService_Warns()
        {
            var (status, alerts, _) = Run(Sfu(new[] {1d}), services: new List<ServiceStatus> {new("rabbitmq", "degraded", null, 1)});

            Assert.Equal("warning", status);
            Assert.Contains("has a problem", alerts.Single().Message);
        }

        [Theory]
        [InlineData(50, "ok")]
        [InlineData(14, "warning")]
        [InlineData(4, "critical")]
        public void RecorderDisk(double freePercent, string expected)
        {
            var recorder = new RecorderStats {Active = 0, MaxConcurrent = 2, Disk = new DiskInfo {FreeBytes = freePercent, TotalBytes = 100}};

            var result = Run(Sfu(new[] {1d}), recorder: recorder);

            Assert.Equal(expected, result.Status);
        }

        [Fact]
        public void RecorderAtItsLimit_Warns()
        {
            var result = Run(Sfu(new[] {1d}), recorder: new RecorderStats {Active = 2, MaxConcurrent = 2});

            Assert.Equal("warning", result.Status);
            Assert.Contains("2 of 2", result.Alerts.Single().Message);
        }

        [Fact]
        public void WorstAlertDecidesTheStatus()
        {
            var (status, alerts, _) = Run(Sfu(new[] {75d, 99d}));

            Assert.Equal("critical", status);
            Assert.Equal(2, alerts.Count);
        }
    }
}
