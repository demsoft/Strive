using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.Core.Services.Videos
{
    /// <summary>
    ///     Who may watch a video: the creator, everybody if it is free, people who bought it, and people with a running
    ///     pass of the creator.
    /// </summary>
    public class VideoAccess
    {
        private readonly IVideoEntitlementRepo _entitlements;
        private readonly IPassRepo _passes;
        private readonly TimeProvider _timeProvider;

        public VideoAccess(IVideoEntitlementRepo entitlements, IPassRepo passes, TimeProvider timeProvider)
        {
            _entitlements = entitlements;
            _passes = passes;
            _timeProvider = timeProvider;
        }

        public async Task<bool> CanWatch(Video video, string? userId) =>
            (await Unlocked(new[] {video}, userId)).Contains(video.VideoId);

        /// <summary>The ids of these videos that the person may watch (no answer per video: lists need few questions)</summary>
        public async Task<IReadOnlySet<string>> Unlocked(IReadOnlyCollection<Video> videos, string? userId)
        {
            var result = new HashSet<string>(videos.Where(x => x.PriceMinor <= 0).Select(x => x.VideoId));
            if (userId == null) return result;

            var paid = videos.Where(x => x.PriceMinor > 0).ToList();
            foreach (var video in paid.Where(x => x.CreatorUserId == userId)) result.Add(video.VideoId);

            var rest = paid.Where(x => !result.Contains(x.VideoId)).ToList();
            if (rest.Count == 0) return result;

            foreach (var id in await _entitlements.HeldAmong(userId, rest.Select(x => x.VideoId))) result.Add(id);

            // a running pass of the creator covers all their videos: one question per creator
            var now = _timeProvider.GetUtcNow();
            foreach (var creatorId in rest.Where(x => !result.Contains(x.VideoId)).Select(x => x.CreatorId).Distinct())
                if (await _passes.ActiveUntil(userId, creatorId, now) != null)
                    foreach (var video in rest.Where(x => x.CreatorId == creatorId)) result.Add(video.VideoId);

            return result;
        }
    }
}
