using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Sessions;
using Strive.Core.Services.Payments.UseCases;
using Strive.Core.Services.Videos;

namespace Strive.Core.Services.Social
{
    public record DirectoryEntry(CreatorProfile Creator, long Followers, bool Following, int Videos, int Sessions);

    public record DirectoryPage(IReadOnlyList<DirectoryEntry> Items, int Total);

    public static class DirectorySorts
    {
        public const string Popular = "popular";

        public const string New = "new";
    }

    /// <summary>
    ///     Creators for fans to find and follow: every creator, found by name, the most followed first (the ones with
    ///     something to see ahead of the others) or the newest first. Or just the ones the person follows.
    /// </summary>
    public class CreatorDirectory
    {
        /// <summary>A session is for sale until this long after it started (the same as on the pages)</summary>
        private static readonly TimeSpan SessionGrace = TimeSpan.FromMinutes(SessionRules.MaxDurationMinutes);

        private readonly ICreatorRepo _creators;
        private readonly IVideoRepo _videos;
        private readonly ISessionRepo _sessions;
        private readonly IFollowRepo _follows;
        private readonly TimeProvider _timeProvider;

        public CreatorDirectory(ICreatorRepo creators, IVideoRepo videos, ISessionRepo sessions, IFollowRepo follows,
            TimeProvider timeProvider)
        {
            _creators = creators;
            _videos = videos;
            _sessions = sessions;
            _follows = follows;
            _timeProvider = timeProvider;
        }

        public async Task<DirectoryPage> Find(string? userId, string? query, string? sort, bool onlyFollowed, int skip,
            int limit)
        {
            var now = _timeProvider.GetUtcNow();
            // everybody who is a creator is listed: a new creator must be found as well, the ones with something to see
            // come first among the same number of followers
            var profiles = onlyFollowed
                ? (await _creators.FindByIds(userId == null
                    ? Array.Empty<string>()
                    : await _follows.CreatorIdsOfUser(userId, 500))).ToList()
                : (await _creators.FindAll(3000)).ToList();

            var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length > 0)
                profiles = profiles.Where(x => words.All(w =>
                    x.DisplayName.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                    (x.Bio?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false))).ToList();

            var creatorIds = profiles.Select(x => x.CreatorId).ToList();
            var followers = await _follows.CountsOfCreators(creatorIds);
            var videos = await _videos.CountReadyOfCreators(creatorIds);
            var sessions = await _sessions.CountForSaleOfCreators(creatorIds, now - SessionGrace);
            var ordered = string.Equals(sort, DirectorySorts.New, StringComparison.OrdinalIgnoreCase)
                ? profiles.OrderByDescending(x => x.CreatedAt)
                : profiles.OrderByDescending(x => followers.GetValueOrDefault(x.CreatorId))
                    .ThenByDescending(x => videos.GetValueOrDefault(x.CreatorId) + sessions.GetValueOrDefault(x.CreatorId))
                    .ThenByDescending(x => x.CreatedAt);

            var page = ordered.Skip(Math.Max(0, skip)).Take(Math.Clamp(limit, 1, 60)).ToList();
            var pageIds = page.Select(x => x.CreatorId).ToList();
            var followed = userId == null ? new HashSet<string>() : await _follows.FollowedAmong(userId, pageIds);

            return new DirectoryPage(
                page.Select(x => new DirectoryEntry(x, followers.GetValueOrDefault(x.CreatorId),
                    followed.Contains(x.CreatorId), videos.GetValueOrDefault(x.CreatorId),
                    sessions.GetValueOrDefault(x.CreatorId))).ToList(), profiles.Count);
        }
    }
}
