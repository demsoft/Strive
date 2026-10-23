using System;
using System.Collections.Generic;
using Strive.Core.Services.Payments.UseCases;

namespace Strive.Core.Services.Videos
{
    public enum VideoStatus
    {
        /// <summary>The creator is sending the file to the converter</summary>
        Uploading,

        /// <summary>The file arrived and is being converted</summary>
        Processing,

        /// <summary>Converted and stored: it can be played</summary>
        Ready,

        /// <summary>Did not work (too long, not a video, the converter did not finish): the reason is on the video</summary>
        Failed,
    }

    /// <summary>
    ///     A video of a creator (a reel or a lesson). The creator sends the file to the converter (Cloudinary), the server
    ///     copies the converted file to the storage (Cloudflare R2) and plays it from there.
    /// </summary>
    public class Video
    {
        public Video(string videoId, string creatorId, string creatorUserId, string title, DateTimeOffset createdAt)
        {
            VideoId = videoId;
            CreatorId = creatorId;
            CreatorUserId = creatorUserId;
            Title = title;
            CreatedAt = createdAt;
        }

        public string VideoId { get; init; }

        public string CreatorId { get; init; }

        public string CreatorUserId { get; init; }

        public string Title { get; set; }

        public string? Description { get; set; }

        public VideoStatus Status { get; set; } = VideoStatus.Uploading;

        /// <summary>Why it failed, in words the creator can act on</summary>
        public string? FailureReason { get; set; }

        public double? DurationSeconds { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        /// <summary>The cover picture was made and stored</summary>
        public bool HasPoster { get; set; }

        /// <summary>Size of the converted file in the storage</summary>
        public long? SizeBytes { get; set; }

        /// <summary>0: free for everybody. Selling videos comes later.</summary>
        public long PriceMinor { get; set; }

        /// <summary>When the file arrived at the converter: how long it has been converting</summary>
        public DateTimeOffset? ProcessingSince { get; set; }

        /// <summary>How many times the finished file could not be copied to the storage</summary>
        public int CopyAttempts { get; set; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? ReadyAt { get; set; }
    }

    public static class VideoRules
    {
        /// <summary>A reel or a short lesson</summary>
        public const int MaxSeconds = 300;

        /// <summary>What the page may send at most; the plan of the converter may allow less and says so</summary>
        public const long MaxUploadBytes = 1024L * 1024 * 1024;

        public const int MinTitleLength = 3;
        public const int MaxTitleLength = 100;
        public const int MaxDescriptionLength = 2000;
        public const int MaxVideosPerCreator = 200;

        /// <summary>A file that has not arrived after this long was given up</summary>
        public static readonly TimeSpan UploadGivenUpAfter = TimeSpan.FromHours(3);

        /// <summary>A file that was not converted after this long failed</summary>
        public static readonly TimeSpan ConversionGivenUpAfter = TimeSpan.FromHours(2);

        public const int MaxCopyAttempts = 5;

        /// <summary>The price is nothing (free), or between the smallest and the biggest price of a ticket</summary>
        public static bool IsValidPrice(long priceMinor) =>
            priceMinor == 0 || priceMinor is >= SessionRules.MinPriceMinor and <= SessionRules.MaxPriceMinor;

        public static string VideoKey(string videoId) => $"videos/{videoId}/video.mp4";

        public static string PosterKey(string videoId) => $"videos/{videoId}/poster.jpg";
    }

    /// <summary>What the browser needs to send the file straight to the converter</summary>
    /// <param name="Url">Where the file is posted</param>
    /// <param name="Fields">Every field goes into the form, before the file (they are signed: none may change)</param>
    public record UploadTicket(string Url, IReadOnlyDictionary<string, string> Fields);

    /// <summary>What the converter says about a file</summary>
    /// <param name="Exists">The file arrived</param>
    /// <param name="VideoUrl">The converted video, once it is ready</param>
    /// <param name="PosterUrl">The cover picture, once it is ready</param>
    public record ConversionStatus(bool Exists, double? DurationSeconds, long? Bytes, int? Width, int? Height,
        Uri? VideoUrl, Uri? PosterUrl);

    public class VideoServiceException : Exception
    {
        public VideoServiceException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }
}
