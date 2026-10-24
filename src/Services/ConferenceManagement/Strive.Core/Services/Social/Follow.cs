using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Strive.Core.Services.Social
{
    /// <summary>Whether the person gets mail when the creator posts a video or schedules a session</summary>
    public enum NoticeStatus
    {
        Off,

        /// <summary>The address was given, the mail with the link to confirm it is on its way</summary>
        Pending,

        On,
    }

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

        public NoticeStatus Notice { get; set; } = NoticeStatus.Off;

        /// <summary>Where the notices go; only set while they are pending or on</summary>
        public string? NoticeEmail { get; set; }

        /// <summary>The secret in the links of the mails (confirm, stop). Not guessable, never shown to anybody else.</summary>
        public string? NoticeToken { get; set; }

        /// <summary>When the last notice was sent: there are not too many in a row</summary>
        public DateTimeOffset? LastNoticeAt { get; set; }
    }

    public interface IFollowRepo
    {
        Task<Follow?> Find(string userId, string creatorId);

        Task<Follow?> FindByNoticeToken(string token);

        /// <summary>Sets the notice settings of a follow</summary>
        Task SetNotice(string followId, NoticeStatus status, string? email, string? token);

        /// <summary>The followers who get mail, who were not sent one since <paramref name="notNoticedSince" /></summary>
        Task<IReadOnlyList<Follow>> FindNoticeRecipients(string creatorId, DateTimeOffset notNoticedSince, int limit);

        Task MarkNoticed(IEnumerable<string> followIds, DateTimeOffset at);

        /// <summary>Stores the follow. False if the person follows the creator already.</summary>
        Task<bool> TryAdd(Follow follow);

        /// <summary>Removes the follow. False if there was none.</summary>
        Task<bool> Remove(string userId, string creatorId);

        Task<bool> Exists(string userId, string creatorId);

        Task<long> CountOfCreator(string creatorId);

        /// <summary>How many people follow each creator (none: not in the answer)</summary>
        Task<IReadOnlyDictionary<string, long>> CountsOfCreators(IEnumerable<string> creatorIds);

        /// <summary>Of these creators, the ones the person follows</summary>
        Task<IReadOnlySet<string>> FollowedAmong(string userId, IEnumerable<string> creatorIds);

        /// <summary>The creators the person follows, the latest follow first</summary>
        Task<IReadOnlyList<string>> CreatorIdsOfUser(string userId, int limit);
    }
}
