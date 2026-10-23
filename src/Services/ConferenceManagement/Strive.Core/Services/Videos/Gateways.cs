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
        Task<IReadOnlyList<Video>> FindReady(DateTimeOffset? before, int limit);

        Task<IReadOnlyList<Video>> FindReadyOfCreator(string creatorId, int limit);

        /// <summary>Videos in this state, the oldest first (for the background work)</summary>
        Task<IReadOnlyList<Video>> FindByStatus(VideoStatus status, int limit);

        Task Update(Video video);

        Task Delete(string videoId);
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
