using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Videos;

namespace Strive.Core.Services.Social
{
    /// <summary>A person likes a video (once)</summary>
    public class VideoLike
    {
        public VideoLike(string likeId, string videoId, string userId, DateTimeOffset createdAt)
        {
            LikeId = likeId;
            VideoId = videoId;
            UserId = userId;
            CreatedAt = createdAt;
        }

        public string LikeId { get; init; }

        public string VideoId { get; init; }

        public string UserId { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
    }

    public interface IVideoLikeRepo
    {
        /// <summary>Stores the like. False if the person likes the video already.</summary>
        Task<bool> TryAdd(VideoLike like);

        /// <summary>Removes the like. False if there was none.</summary>
        Task<bool> Remove(string userId, string videoId);

        /// <summary>How many people like each video (a video nobody likes is not in the answer)</summary>
        Task<IReadOnlyDictionary<string, long>> CountsOf(IEnumerable<string> videoIds);

        /// <summary>Of these videos, the ones the person likes</summary>
        Task<IReadOnlySet<string>> LikedAmong(string userId, IEnumerable<string> videoIds);

        Task DeleteOfVideo(string videoId);
    }

    /// <summary>A person likes a video. Doing it twice changes nothing.</summary>
    public record LikeVideoRequest(string UserId, string VideoId) : IRequest<SuccessOrError<Unit>>;

    /// <summary>A person takes the like back. Doing it twice changes nothing.</summary>
    public record UnlikeVideoRequest(string UserId, string VideoId) : IRequest<SuccessOrError<Unit>>;

    public class LikeVideoUseCase : IRequestHandler<LikeVideoRequest, SuccessOrError<Unit>>
    {
        private readonly IVideoRepo _videos;
        private readonly IVideoLikeRepo _likes;
        private readonly TimeProvider _timeProvider;

        public LikeVideoUseCase(IVideoRepo videos, IVideoLikeRepo likes, TimeProvider timeProvider)
        {
            _videos = videos;
            _likes = likes;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<Unit>> Handle(LikeVideoRequest request, CancellationToken cancellationToken)
        {
            var video = await _videos.FindById(request.VideoId);
            // what a person cannot see, they cannot like
            if (video == null || !(video.IsPublic || video.CreatorUserId == request.UserId))
                return VideosError.NotFound;

            await _likes.TryAdd(new VideoLike(Guid.NewGuid().ToString("N"), video.VideoId, request.UserId,
                _timeProvider.GetUtcNow()));
            return Unit.Value;
        }
    }

    public class UnlikeVideoUseCase : IRequestHandler<UnlikeVideoRequest, SuccessOrError<Unit>>
    {
        private readonly IVideoLikeRepo _likes;

        public UnlikeVideoUseCase(IVideoLikeRepo likes)
        {
            _likes = likes;
        }

        public async Task<SuccessOrError<Unit>> Handle(UnlikeVideoRequest request, CancellationToken cancellationToken)
        {
            await _likes.Remove(request.UserId, request.VideoId);
            return Unit.Value;
        }
    }
}
