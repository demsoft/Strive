namespace Strive.Infrastructure.Recording
{
    /// <summary>
    ///     Settings of the S3 compatible storage (section "Recording:Storage"): Cloudflare R2, MinIO, AWS S3, ...
    /// </summary>
    public class RecordingStorageOptions
    {
        /// <summary>
        ///     The endpoint, e.g. https://&lt;account id&gt;.r2.cloudflarestorage.com or http://minio:9000
        /// </summary>
        public string? ServiceUrl { get; set; }

        public string Bucket { get; set; } = "strive-recordings";

        public string? AccessKeyId { get; set; }

        public string? SecretAccessKey { get; set; }

        /// <summary>
        ///     "auto" for Cloudflare R2
        /// </summary>
        public string Region { get; set; } = "auto";

        /// <summary>
        ///     Required by MinIO
        /// </summary>
        public bool ForcePathStyle { get; set; }
    }
}
