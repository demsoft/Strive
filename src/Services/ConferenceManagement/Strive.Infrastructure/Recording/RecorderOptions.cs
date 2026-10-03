namespace Strive.Infrastructure.Recording
{
    /// <summary>
    ///     Settings of the connection to the recorder service (section "Recording:Recorder")
    /// </summary>
    public class RecorderOptions
    {
        /// <summary>
        ///     Where the recorder service listens, e.g. http://recorder:3000
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        ///     Shared by the server and the recorder, each request to the other side must contain it
        /// </summary>
        public string? SharedSecret { get; set; }

        /// <summary>
        ///     Signs the tokens recorders use to join conferences
        /// </summary>
        public string? TokenSecret { get; set; }

        public const string TokenIssuer = "strive-recorder";
        public const string TokenAudience = "strive-conference";
        public const string RoleClaimValue = "recorder";
        public const string SecretHeader = "X-Recorder-Secret";
    }
}
