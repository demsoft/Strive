using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Strive.Core.Services.Videos
{
    public interface IVideoRepo
    {
        Task Create(Video video);

        Task<Video?> FindById(string videoId);

        Task<IReadOnlyList<Video>> FindOfCreator(string creatorId, int limit);

        Task<long> CountOfCreator(string creatorId);

        /// <summary>Ready videos, newest first. With a time: only the ones that became ready before it (the next page).</summary>
        /// <param name="creatorIds">Only the videos of these creators; none: everybody's</param>
        Task<IReadOnlyList<Video>> FindReady(DateTimeOffset? before, int limit,
            IReadOnlyCollection<string>? creatorIds = null);

        Task<IReadOnlyList<Video>> FindReadyOfCreator(string creatorId, int limit);

        Task<IReadOnlyList<Video>> FindByIds(IEnumerable<string> videoIds);

        /// <summary>Videos that became ready since the time, are shown, and whose followers were not told yet</summary>
        Task<IReadOnlyList<Video>> FindForFollowerNotice(DateTimeOffset readySince, int limit);

        /// <summary>Marks the notice as taken. Only the first call for a video returns true.</summary>
        Task<bool> TryClaimFollowerNotice(string videoId, DateTimeOffset at);

        /// <summary>Videos that are hidden or removed (for the moderators), newest first</summary>
        Task<IReadOnlyList<Video>> FindModerated(int limit);

        /// <summary>Videos in this state, the oldest first (for the background work)</summary>
        Task<IReadOnlyList<Video>> FindByStatus(VideoStatus status, int limit);

        Task Update(Video video);

        Task Delete(string videoId);
    }

    public interface IVideoReportRepo
    {
        /// <summary>Stores the report. A person reports a video once: false if they already had.</summary>
        Task<bool> TryAdd(VideoReport report);

        /// <summary>How many different people reported the video and it has not been decided yet</summary>
        Task<int> CountOpen(string videoId);

        Task<IReadOnlyList<VideoReport>> FindOpen(int limit);

        Task<IReadOnlyList<VideoReport>> FindOpenOfVideo(string videoId);

        /// <summary>A moderator decided: the open reports of the video no longer count</summary>
        Task ResolveOpen(string videoId, DateTimeOffset at);
    }

    /// <summary>The right of a person to watch a video they bought. One per paid order.</summary>
    public class VideoEntitlement
    {
        public VideoEntitlement(string entitlementId, string userId, string videoId, string orderId,
            DateTimeOffset grantedAt)
        {
            EntitlementId = entitlementId;
            UserId = userId;
            VideoId = videoId;
            OrderId = orderId;
            GrantedAt = grantedAt;
        }

        public string EntitlementId { get; init; }

        public string UserId { get; init; }

        public string VideoId { get; init; }

        public string OrderId { get; init; }

        public DateTimeOffset GrantedAt { get; init; }
    }

    public interface IVideoEntitlementRepo
    {
        /// <summary>Grants the right once per order: a second call with the same order changes nothing</summary>
        /// <returns>True if the right was new</returns>
        Task<bool> GrantOnce(VideoEntitlement entitlement);

        Task<bool> Holds(string userId, string videoId);

        /// <summary>Which of these videos the person bought</summary>
        Task<IReadOnlyList<string>> HeldAmong(string userId, IEnumerable<string> videoIds);

        Task<IReadOnlyList<VideoEntitlement>> FindOfUser(string userId, int limit);

        Task<long> CountOfVideo(string videoId);
    }

    /// <summary>The converter (Cloudinary): takes the file the creator sends and makes a video every browser plays</summary>
    public interface IVideoConverter
    {
        bool IsConfigured { get; }

        /// <summary>A signed form for one video: the file goes straight from the browser to the converter</summary>
        UploadTicket CreateUploadTicket(string videoId);

        /// <summary>The file with this video id at the converter: has it arrived, how long is it, is the conversion finished</summary>
        Task<ConversionStatus> GetStatus(string videoId, CancellationToken cancellationToken = default);

        /// <summary>Opens the converted file or the cover picture for reading. Dispose the result.</summary>
        Task<RemoteFile> Open(Uri url, CancellationToken cancellationToken = default);

        /// <summary>Deletes the file at the converter (what was sent and what was made of it)</summary>
        Task Delete(string videoId, CancellationToken cancellationToken = default);
    }

    public sealed class RemoteFile : IDisposable
    {
        public RemoteFile(Stream content, long length, IDisposable? owner = null)
        {
            Content = content;
            Length = length;
            _owner = owner;
        }

        private readonly IDisposable? _owner;

        public Stream Content { get; }

        public long Length { get; }

        public void Dispose()
        {
            Content.Dispose();
            _owner?.Dispose();
        }
    }

    /// <summary>The storage of the converted videos (Cloudflare R2): the server puts them there and hands out temporary links</summary>
    public interface IVideoStorage
    {
        bool IsConfigured { get; }

        Task Put(string key, Stream content, long length, string contentType,
            CancellationToken cancellationToken = default);

        /// <summary>A temporary link that plays the file in a browser (with seeking)</summary>
        Uri GetPlaybackUrl(string key, TimeSpan validFor, string contentType);

        Task Delete(string key, CancellationToken cancellationToken = default);
    }
}
