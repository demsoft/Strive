namespace Strive.Core.Services.Recording
{
    public enum RecordingStatus
    {
        /// <summary>
        ///     The recorder was asked to join the conference
        /// </summary>
        Starting,

        /// <summary>
        ///     The recorder is joined and recording
        /// </summary>
        Recording,

        /// <summary>
        ///     Recording was stopped, the recorder finishes and uploads the file
        /// </summary>
        Finalizing,

        /// <summary>
        ///     The file is stored and can be played
        /// </summary>
        Ready,
        Failed,
    }

    public enum RecordingVisibility
    {
        /// <summary>
        ///     Only signed in users that have the link can watch
        /// </summary>
        SignedIn,

        /// <summary>
        ///     Everybody that has the link can watch
        /// </summary>
        AnyoneWithLink,
    }
}
