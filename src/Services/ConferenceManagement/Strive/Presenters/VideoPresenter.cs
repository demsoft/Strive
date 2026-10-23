using System;
using Strive.Core.Services.Videos;
using Strive.Models.Response;

namespace Strive.Presenters
{
    /// <summary>Makes what the page needs of a video: with temporary links to the cover and (if wanted) to the video.</summary>
    public class VideoPresenter
    {
        /// <summary>A link plays a video for this long (the page asks for a new one when a person comes back)</summary>
        public static readonly TimeSpan PlaybackLinkValidFor = TimeSpan.FromHours(2);

        public static readonly TimeSpan PosterLinkValidFor = TimeSpan.FromHours(6);

        private readonly IVideoStorage _storage;

        public VideoPresenter(IVideoStorage storage)
        {
            _storage = storage;
        }

        public VideoDto ToDto(Video video, PublicCreatorDto? creator, bool isOwner, bool withPlayback)
        {
            var ready = video.Status == VideoStatus.Ready && _storage.IsConfigured;
            return new VideoDto(video.VideoId, video.Title, video.Description, video.Status.ToString().ToLowerInvariant(),
                isOwner ? video.FailureReason : null, video.DurationSeconds, video.Width, video.Height, video.PriceMinor,
                video.CreatedAt, isOwner,
                ready && video.HasPoster
                    ? _storage.GetPlaybackUrl(VideoRules.PosterKey(video.VideoId), PosterLinkValidFor, "image/jpeg")
                        .ToString()
                    : null,
                ready && withPlayback
                    ? _storage.GetPlaybackUrl(VideoRules.VideoKey(video.VideoId), PlaybackLinkValidFor, "video/mp4")
                        .ToString()
                    : null,
                creator);
        }
    }
}
