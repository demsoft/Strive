using System;
using System.Collections.Generic;
using Strive.Core.Services.Videos;

namespace Strive.Models.Response
{
    public record CreateVideoDto(string Title, string? Description, long SizeBytes);

    /// <summary>The form the page sends the file with: every field before the file, all of them unchanged</summary>
    public record UploadTicketDto(string Url, IReadOnlyDictionary<string, string> Fields);

    public record CreatedVideoDto(VideoDto Video, UploadTicketDto Ticket);

    /// <param name="Next">Ask for the videos before this time to get the next page; null: that was all</param>
    public record VideoFeedDto(VideoDto[] Items, DateTimeOffset? Next);

    /// <param name="Status">uploading, processing, ready or failed</param>
    /// <param name="FailureReason">Only the creator sees why it failed</param>
    /// <param name="PosterUrl">A temporary link to the cover picture</param>
    /// <param name="PlaybackUrl">A temporary link that plays the video: only in the answer for a single video, and only for people who may watch it</param>
    public record VideoDto(string VideoId, string Title, string? Description, string Status, string? FailureReason,
        double? DurationSeconds, int? Width, int? Height, long PriceMinor, DateTimeOffset CreatedAt, bool IsOwner,
        string? PosterUrl, string? PlaybackUrl, PublicCreatorDto? Creator);
}
