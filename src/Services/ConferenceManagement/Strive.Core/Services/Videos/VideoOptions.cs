namespace Strive.Core.Services.Videos
{
    /// <summary>Settings of the videos (configuration section "Videos")</summary>
    public class VideoOptions
    {
        public const string Section = "Videos";

        /// <summary>The folder at the converter (Cloudinary) the files are sent to</summary>
        public string Folder { get; set; } = "gbedu/videos";

        /// <summary>This many different people reporting a video hides it until a moderator has looked at it</summary>
        public int ReportsToHide { get; set; } = ReportRules.DefaultReportsToHide;
    }

    /// <summary>The storage of the converted videos (section "Videos:Storage"): Cloudflare R2, or any S3 compatible store</summary>
    public class VideoStorageOptions
    {
        public string? ServiceUrl { get; set; }

        public string Bucket { get; set; } = "gbedu-videos";

        public string? AccessKeyId { get; set; }

        public string? SecretAccessKey { get; set; }

        /// <summary>"auto" for Cloudflare R2</summary>
        public string Region { get; set; } = "auto";

        public bool ForcePathStyle { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceUrl) && !string.IsNullOrWhiteSpace(AccessKeyId) &&
                                    !string.IsNullOrWhiteSpace(SecretAccessKey) && !string.IsNullOrWhiteSpace(Bucket);
    }
}
