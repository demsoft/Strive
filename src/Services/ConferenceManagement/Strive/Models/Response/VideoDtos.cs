using System;
using System.Collections.Generic;
using Strive.Core.Services.Videos;

namespace Strive.Models.Response
{
    public record ReportDetailDto(string Reason, string? Text, DateTimeOffset At);

    /// <summary>A video that people reported (or that is hidden), as the moderators see it</summary>
    /// <param name="Buyers">How many people bought it: removing a video they paid for is for the moderators to make right</param>
    /// <param name="PlaybackUrl">A temporary link for the moderator to watch it, even while it is hidden</param>
    public record AdminVideoReportDto(string VideoId, string Title, string CreatorName, string Moderation,
        long PriceMinor, long Buyers, int OpenReports, System.Collections.Generic.IReadOnlyDictionary<string, int> Reasons,
        System.Collections.Generic.IReadOnlyList<ReportDetailDto> Details, DateTimeOffset? LatestReportAt,
        string? PosterUrl, string? PlaybackUrl);

    public record ModerateVideoDto(string? Note);

    public record ReportVideoDto(string Reason, string? Details);

    public record CreateVideoDto(string Title, string? Description, long SizeBytes, long PriceMinor = 0);

    public record UpdateVideoDto(string Title, string? Description, long PriceMinor);

    /// <summary>The form the page sends the file with: every field before the file, all of them unchanged</summary>
    public record UploadTicketDto(string Url, IReadOnlyDictionary<string, string> Fields);

    public record CreatedVideoDto(VideoDto Video, UploadTicketDto Ticket);

    /// <param name="Next">Ask for the videos before this time to get the next page; null: that was all</param>
    public record VideoFeedDto(VideoDto[] Items, DateTimeOffset? Next);

    /// <param name="Status">uploading, processing, ready or failed</param>
    /// <param name="FailureReason">Only the creator sees why it failed</param>
    /// <param name="PosterUrl">A temporary link to the cover picture</param>
    /// <param name="Moderation">none, hidden (reported, out of sight until a moderator looked at it) or removed. Only the creator sees it.</param>
    /// <param name="Locked">It costs money and the person has not paid (and has no pass): there is no playback link</param>
    /// <param name="PlaybackUrl">A temporary link that plays the video: only in the answer for a single video, and only for people who may watch it</param>
    public record VideoDto(string VideoId, string Title, string? Description, string Status, string? FailureReason,
        double? DurationSeconds, int? Width, int? Height, long PriceMinor, DateTimeOffset CreatedAt, bool IsOwner,
        string? PosterUrl, string? PlaybackUrl, PublicCreatorDto? Creator, bool Locked = false,
        string Moderation = "none", string? ModerationNote = null);
}
