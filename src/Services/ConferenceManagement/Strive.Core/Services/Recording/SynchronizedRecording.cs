using System;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Recording
{
    /// <summary>
    ///     Whether the conference is recorded right now. Visible to every participant, recording is never secret.
    /// </summary>
    public record SynchronizedRecording(SynchronizedRecording.ActiveRecording? Active)
    {
        public static readonly SynchronizedObjectId SyncObjId = new(SynchronizedObjectIds.RECORDING);

        public record ActiveRecording(string RecordingId, RecordingStatus Status, DateTimeOffset StartedAt,
            string StartedBy);
    }
}
