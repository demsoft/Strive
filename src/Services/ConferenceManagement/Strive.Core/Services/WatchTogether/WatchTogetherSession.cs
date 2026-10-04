using System;

namespace Strive.Core.Services.WatchTogether
{
    public enum WatchTogetherPlaybackState
    {
        Playing,
        Paused,
    }

    /// <summary>
    ///     A video that the participants of a conference watch together. The playback is described by the position at the
    ///     time of the last change, so that every client can compute where the video is right now.
    /// </summary>
    /// <param name="Provider">Where the video is played from, "youtube"</param>
    /// <param name="VideoId">The id of the video at the provider</param>
    /// <param name="StartedBy">Participant id of the participant that started it</param>
    /// <param name="PositionSeconds">Position of the video at <paramref name="UpdatedAt" /></param>
    /// <param name="Rate">Playback speed</param>
    /// <param name="UpdatedAt">Time of the last play, pause, seek or speed change (server time)</param>
    public record WatchTogetherSession(string Provider, string VideoId, string StartedBy, DateTimeOffset StartedAt,
        WatchTogetherPlaybackState State, double PositionSeconds, double Rate, DateTimeOffset UpdatedAt)
    {
        public const string YouTube = "youtube";

        /// <summary>The position of the video at the given time</summary>
        public double PositionAt(DateTimeOffset now)
        {
            if (State == WatchTogetherPlaybackState.Paused) return PositionSeconds;

            var elapsed = Math.Max(0, (now - UpdatedAt).TotalSeconds);
            return PositionSeconds + elapsed * Rate;
        }
    }
}
