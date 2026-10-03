using System;

namespace Strive.Core.Services.Recording
{
    /// <summary>
    ///     A recording of a conference. Always started deliberately by a moderator.
    /// </summary>
    public class ConferenceRecording
    {
        public ConferenceRecording(string recordingId, string conferenceId, string startedBy, DateTimeOffset startedAt,
            DateTimeOffset expiresAt, string shareToken)
        {
            RecordingId = recordingId;
            ConferenceId = conferenceId;
            StartedBy = startedBy;
            StartedAt = startedAt;
            ExpiresAt = expiresAt;
            ShareToken = shareToken;
        }

        public string RecordingId { get; init; }

        public string ConferenceId { get; init; }

        /// <summary>
        ///     The participant id of the moderator that started the recording
        /// </summary>
        public string StartedBy { get; init; }

        public DateTimeOffset StartedAt { get; init; }

        public DateTimeOffset? EndedAt { get; set; }

        public RecordingStatus Status { get; set; } = RecordingStatus.Starting;

        /// <summary>
        ///     The key of the file in the storage, set when the recorder finished
        /// </summary>
        /// <summary>
        ///     True until the recording is finished. Stored in the database so that "at most one active recording per
        ///     conference" can be enforced by a unique index.
        /// </summary>
        public bool IsActive => Status is RecordingStatus.Starting or RecordingStatus.Recording
            or RecordingStatus.Finalizing;

        public string? StorageKey { get; set; }

        public long? SizeBytes { get; set; }

        public double? DurationSeconds { get; set; }

        public string? FailureReason { get; set; }

        public RecordingVisibility Visibility { get; set; } = RecordingVisibility.SignedIn;

        /// <summary>
        ///     The secret part of the share link
        /// </summary>
        public string ShareToken { get; init; }

        /// <summary>
        ///     The recording is deleted after this date
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
