using System;

namespace Strive.Admin
{
    /// <summary>Settings of the admin overview (section "Admin").</summary>
    public class AdminOptions
    {
        public const string Section = "Admin";
        public const string ServerAdminRole = "serveradmin";
        public const string Policy = "ServerAdmin";

        /// <summary>Internal address of the media server, for example http://sfu:3000. Empty: not part of the overview.</summary>
        public string? SfuUrl { get; set; }

        /// <summary>Internal address of the identity service, for example http://identity-api</summary>
        public string? IdentityUrl { get; set; }

        /// <summary>
        ///     Internal address of the recorder. Empty: the recorder of the recording settings
        ///     (Recording:Recorder:BaseUrl) is used if recording is enabled.
        /// </summary>
        public string? RecorderUrl { get; set; }

        /// <summary>The key the media server and the identity service expect (header X-Api-Key).</summary>
        public string? ApiKey { get; set; }

        /// <summary>Write the numbers of the overview to the database every minute, for the charts of the last days.</summary>
        public bool SamplingEnabled { get; set; } = true;

        public TimeSpan SampleInterval { get; set; } = TimeSpan.FromMinutes(1);
        public int HistoryDays { get; set; } = 7;

        public AdminThresholds Thresholds { get; set; } = new();
    }

    public class AdminThresholds
    {
        /// <summary>A media worker is one process on one core: when it is busy no more conferences fit on it.</summary>
        public double WorkerCpuWarningPercent { get; set; } = 70;

        public double WorkerCpuCriticalPercent { get; set; } = 90;

        public double MemoryWarningPercent { get; set; } = 80;
        public double MemoryCriticalPercent { get; set; } = 92;

        /// <summary>The one minute load average divided by the number of cores</summary>
        public double LoadPerCoreWarning { get; set; } = 0.8;

        public double LoadPerCoreCritical { get; set; } = 1.2;

        public double DiskFreeWarningPercent { get; set; } = 15;
        public double DiskFreeCriticalPercent { get; set; } = 5;
    }
}
