namespace Strive.Core.Services.Recording
{
    /// <summary>
    ///     The recorder joins a conference as a participant. Its id cannot collide with real participant ids (hex encoded
    ///     user names or generated user ids).
    /// </summary>
    public static class RecorderParticipants
    {
        public const string Prefix = "recorder-";

        public const string DisplayName = "Recording";

        public static string ParticipantId(string recordingId)
        {
            return Prefix + recordingId;
        }

        public static bool IsRecorder(string participantId)
        {
            return participantId.StartsWith(Prefix, System.StringComparison.Ordinal);
        }
    }
}
