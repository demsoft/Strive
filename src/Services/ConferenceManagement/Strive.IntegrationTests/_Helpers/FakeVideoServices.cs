using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Strive.Core.Services.Videos;

namespace Strive.IntegrationTests._Helpers
{
    /// <summary>Stands in for Cloudinary: a test says what "arrived" and what is "converted".</summary>
    public class FakeVideoConverter : IVideoConverter
    {
        public class Remote
        {
            public double? Duration { get; set; } = 60;
            public bool VideoReady { get; set; }
            public bool PosterReady { get; set; }
        }

        public bool Configured { get; set; } = true;

        public ConcurrentDictionary<string, Remote> Files { get; } = new();

        public ConcurrentQueue<string> Deleted { get; } = new();

        public bool Unreachable { get; set; }

        public bool IsConfigured => Configured;

        public static readonly byte[] VideoBytes = Enumerable.Repeat((byte) 7, 5000).ToArray();
        public static readonly byte[] PosterBytes = Enumerable.Repeat((byte) 9, 300).ToArray();

        public UploadTicket CreateUploadTicket(string videoId) =>
            new("https://converter.test/video/upload", new Dictionary<string, string>
            {
                ["public_id"] = "gbedu/videos/" + videoId, ["timestamp"] = "1700000000", ["api_key"] = "key",
                ["signature"] = "sig", ["eager"] = "c_limit",
            });

        /// <summary>The creator's file arrived at the converter</summary>
        public void Receive(string videoId, double? duration = 60) => Files[videoId] = new Remote {Duration = duration};

        public void Finish(string videoId, bool poster = true)
        {
            Files[videoId].VideoReady = true;
            Files[videoId].PosterReady = poster;
        }

        public Task<ConversionStatus> GetStatus(string videoId, CancellationToken cancellationToken = default)
        {
            if (Unreachable) throw new VideoServiceException("Cloudinary could not be reached.");
            if (!Files.TryGetValue(videoId, out var file))
                return Task.FromResult(new ConversionStatus(false, null, null, null, null, null, null));

            return Task.FromResult(new ConversionStatus(true, file.Duration, 123456, 720, 1280,
                file.VideoReady ? new Uri("https://res.cloudinary.test/video/" + videoId + ".mp4") : null,
                file.PosterReady ? new Uri("https://res.cloudinary.test/video/" + videoId + ".jpg") : null));
        }

        public Task<RemoteFile> Open(Uri url, CancellationToken cancellationToken = default)
        {
            if (Unreachable) throw new VideoServiceException("Cloudinary could not be reached.");
            var bytes = url.AbsolutePath.EndsWith(".jpg") ? PosterBytes : VideoBytes;
            return Task.FromResult(new RemoteFile(new MemoryStream(bytes), bytes.Length));
        }

        public Task Delete(string videoId, CancellationToken cancellationToken = default)
        {
            Deleted.Enqueue(videoId);
            Files.TryRemove(videoId, out _);
            return Task.CompletedTask;
        }
    }

    /// <summary>Stands in for R2: it keeps what was put in it.</summary>
    public class FakeVideoStorage : IVideoStorage
    {
        public ConcurrentDictionary<string, (byte[] Data, string ContentType)> Objects { get; } = new();

        public ConcurrentQueue<string> Deleted { get; } = new();

        public bool Configured { get; set; } = true;

        public bool Failing { get; set; }

        public bool IsConfigured => Configured;

        public Task Put(string key, Stream content, long length, string contentType,
            CancellationToken cancellationToken = default)
        {
            if (Failing) throw new VideoServiceException("The video could not be stored.");
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            if (buffer.Length != length) throw new IOException("The length does not match.");
            Objects[key] = (buffer.ToArray(), contentType);
            return Task.CompletedTask;
        }

        public Uri GetPlaybackUrl(string key, TimeSpan validFor, string contentType) =>
            new($"https://r2.test/{key}?expires={(int) validFor.TotalSeconds}&type={Uri.EscapeDataString(contentType)}");

        public Task Delete(string key, CancellationToken cancellationToken = default)
        {
            Deleted.Enqueue(key);
            Objects.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}
