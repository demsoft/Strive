using System;
using Strive.Core.Services.Recording;

namespace Strive.Models.Response
{
    /// <summary>
    ///     A recording as shown to moderators of the conference
    /// </summary>
    public record RecordingDto(string RecordingId, RecordingStatus Status, DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt, double? DurationSeconds, long? SizeBytes, RecordingVisibility Visibility,
        string ShareToken, DateTimeOffset ExpiresAt, string? FailureReason, RecordingKind Kind = RecordingKind.Meeting,
        bool HasTracks = false)
    {
        public static RecordingDto From(ConferenceRecording recording)
        {
            return new RecordingDto(recording.RecordingId, recording.Status, recording.StartedAt, recording.EndedAt,
                recording.DurationSeconds, recording.SizeBytes, recording.Visibility, recording.ShareToken,
                recording.ExpiresAt, recording.FailureReason, recording.Kind, recording.TracksStorageKey != null);
        }
    }

    /// <summary>A recording in the list of the person that started it, with the conference it belongs to</summary>
    public record MyRecordingDto(string RecordingId, string ConferenceId, string? ConferenceName,
        RecordingStatus Status, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, double? DurationSeconds,
        long? SizeBytes, RecordingVisibility Visibility, string ShareToken, DateTimeOffset ExpiresAt,
        string? FailureReason, RecordingKind Kind = RecordingKind.Meeting, bool HasTracks = false);

    /// <summary>
    ///     What the share page needs to play a recording
    /// </summary>
    public record SharedRecordingDto(string? ConferenceName, DateTimeOffset StartedAt, double? DurationSeconds,
        string Url, DateTimeOffset UrlExpiresAt, RecordingKind Kind = RecordingKind.Meeting,
        string? TracksUrl = null, long? TracksSizeBytes = null);
}
