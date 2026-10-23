using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Strive.Core.Services.Social
{
    /// <summary>A person follows a creator: their videos can be watched on their own, without the rest</summary>
    public class Follow
    {
        public Follow(string followId, string userId, string creatorId, DateTimeOffset createdAt)
        {
            FollowId = followId;
            UserId = userId;
            CreatorId = creatorId;
            CreatedAt = createdAt;
        }

        public string FollowId { get; init; }

        public string UserId { get; init; }

        public string CreatorId { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
    }

    public interface IFollowRepo
    {
        /// <summary>Stores the follow. False if the person follows the creator already.</summary>
        Task<bool> TryAdd(Follow follow);

        /// <summary>Removes the follow. False if there was none.</summary>
        Task<bool> Remove(string userId, string creatorId);

        Task<bool> Exists(string userId, string creatorId);

        Task<long> CountOfCreator(string creatorId);

        /// <summary>The creators the person follows, the latest follow first</summary>
        Task<IReadOnlyList<string>> CreatorIdsOfUser(string userId, int limit);
    }
}
