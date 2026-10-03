using System;
using Strive.Core.Services.Recording;

namespace Strive.Models.Response
{
    /// <summary>
    ///     A recording as shown to moderators of the conference
    /// </summary>
    public record RecordingDto(string RecordingId, RecordingStatus Status, DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt, double? DurationSeconds, long? SizeBytes, RecordingVisibility Visibility,
        string ShareToken, DateTimeOffset ExpiresAt, string? FailureReason)
    {
        public static RecordingDto From(ConferenceRecording recording)
        {
            return new RecordingDto(recording.RecordingId, recording.Status, recording.StartedAt, recording.EndedAt,
                recording.DurationSeconds, recording.SizeBytes, recording.Visibility, recording.ShareToken,
                recording.ExpiresAt, recording.FailureReason);
        }
    }

    /// <summary>
    ///     What the share page needs to play a recording
    /// </summary>
    public record SharedRecordingDto(string? ConferenceName, DateTimeOffset StartedAt, double? DurationSeconds,
        string Url, DateTimeOffset UrlExpiresAt);
}
