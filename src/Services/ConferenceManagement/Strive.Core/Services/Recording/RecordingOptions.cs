using System;

namespace Strive.Core.Services.Recording
{
    /// <summary>
    ///     Server settings for recordings (section "Recording")
    /// </summary>
    public class RecordingOptions
    {
        /// <summary>
        ///     Switch the whole feature on. Off by default, recording needs a recorder service and a storage.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        ///     Recordings are deleted after this many days
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        ///     A recording is stopped automatically after this time
        /// </summary>
        public int MaxDurationMinutes { get; set; } = 240;

        /// <summary>
        ///     How long a playback link is valid. The player requests a new one when it expires.
        /// </summary>
        public TimeSpan PlaybackUrlValidFor { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    ///     Conference setting: recordings may be started in this conference
    /// </summary>
    public class ConferenceRecordingOptions
    {
        public bool IsEnabled { get; set; } = true;
    }
}
