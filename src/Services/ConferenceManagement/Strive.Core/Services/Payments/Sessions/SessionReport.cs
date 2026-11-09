using System;

namespace Strive.Core.Services.Payments.Sessions
{
    public enum SessionReportReason
    {
        /// <summary>The reporter owns the rights to what is shown</summary>
        RightsHolder,

        /// <summary>Against the law</summary>
        Illegal,

        /// <summary>Not what the page says it is</summary>
        Misleading,

        /// <summary>Violence, hate or abuse</summary>
        Abuse,

        Spam,
        Other,
    }

    /// <summary>A person's report of a session. One per person and session.</summary>
    public class SessionReport
    {
        public SessionReport(string reportId, string sessionId, string reporterUserId, SessionReportReason reason,
            string? details, DateTimeOffset createdAt)
        {
            ReportId = reportId;
            SessionId = sessionId;
            ReporterUserId = reporterUserId;
            Reason = reason;
            Details = details;
            CreatedAt = createdAt;
        }

        public string ReportId { get; init; }

        public string SessionId { get; init; }

        public string ReporterUserId { get; init; }

        public SessionReportReason Reason { get; init; }

        public string? Details { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>A moderator has decided: it no longer counts</summary>
        public bool Resolved { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }
    }

    public static class SessionReportRules
    {
        public const int MaxDetailsLength = 500;
    }
}
