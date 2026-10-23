using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strive.Core.Services.Social;
using Strive.Models.Response;

namespace Strive.Presenters
{
    /// <summary>Adds what people did with the videos to them: how many like each, and whether the asker does.</summary>
    public class EngagementPresenter
    {
        private readonly IVideoLikeRepo _likes;

        public EngagementPresenter(IVideoLikeRepo likes)
        {
            _likes = likes;
        }

        public async Task<VideoDto[]> Apply(IReadOnlyCollection<VideoDto> videos, string? userId)
        {
            if (videos.Count == 0) return videos.ToArray();

            var ids = videos.Select(x => x.VideoId).ToList();
            var counts = await _likes.CountsOf(ids);
            var liked = userId == null ? new HashSet<string>() : await _likes.LikedAmong(userId, ids);
            return videos.Select(x => x with
            {
                Likes = counts.TryGetValue(x.VideoId, out var count) ? count : 0, Liked = liked.Contains(x.VideoId),
            }).ToArray();
        }

        public async Task<VideoDto> Apply(VideoDto video, string? userId) => (await Apply(new[] {video}, userId))[0];
    }
}
