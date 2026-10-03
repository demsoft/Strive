namespace Strive.Infrastructure.Recording
{
    /// <summary>
    ///     Settings of the S3 compatible storage (section "Recording:Storage"): Cloudflare R2, AWS S3, a local gateway for development, ...
    /// </summary>
    public class RecordingStorageOptions
    {
        /// <summary>
        ///     The endpoint, e.g. https://&lt;account id&gt;.r2.cloudflarestorage.com
        /// </summary>
        public string? ServiceUrl { get; set; }

        /// <summary>
        ///     The address browsers use to play recordings, if it differs from <see cref="ServiceUrl" />
        /// </summary>
        public string? PublicServiceUrl { get; set; }

        public string Bucket { get; set; } = "strive-recordings";

        public string? AccessKeyId { get; set; }

        public string? SecretAccessKey { get; set; }

        /// <summary>
        ///     "auto" for Cloudflare R2
        /// </summary>
        public string Region { get; set; } = "auto";

        /// <summary>
        ///     Required by the local storage of the development setup, R2 and AWS S3 use virtual hosted style addressing
        /// </summary>
        public bool ForcePathStyle { get; set; }
    }
}
