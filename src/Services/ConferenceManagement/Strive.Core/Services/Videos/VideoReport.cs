using System;

namespace Strive.Core.Services.Videos
{
    public enum ReportReason
    {
        /// <summary>Against the law</summary>
        Illegal,

        /// <summary>Not the creator's own work</summary>
        Copyright,

        /// <summary>Sexual content</summary>
        Sexual,

        /// <summary>Violence or hate</summary>
        Violence,

        /// <summary>A scam, or spam</summary>
        Spam,

        Other,
    }

    /// <summary>A person's report of a video. One per person and video.</summary>
    public class VideoReport
    {
        public VideoReport(string reportId, string videoId, string reporterUserId, ReportReason reason,
            string? details, DateTimeOffset createdAt)
        {
            ReportId = reportId;
            VideoId = videoId;
            ReporterUserId = reporterUserId;
            Reason = reason;
            Details = details;
            CreatedAt = createdAt;
        }

        public string ReportId { get; init; }

        public string VideoId { get; init; }

        public string ReporterUserId { get; init; }

        public ReportReason Reason { get; init; }

        public string? Details { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>A moderator has decided: it no longer counts</summary>
        public bool Resolved { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }
    }

    public static class ReportRules
    {
        public const int MaxDetailsLength = 500;

        /// <summary>This many different people reporting a video hides it until a moderator has looked at it</summary>
        public const int DefaultReportsToHide = 3;
    }
}
