using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Videos;

namespace Strive.Core.Services.Social
{
    /// <summary>What a person wrote under a video. The name is kept as it was when they wrote it.</summary>
    public class VideoComment
    {
        public VideoComment(string commentId, string videoId, string userId, string authorName, string text,
            DateTimeOffset createdAt)
        {
            CommentId = commentId;
            VideoId = videoId;
            UserId = userId;
            AuthorName = authorName;
            Text = text;
            CreatedAt = createdAt;
        }

        public string CommentId { get; init; }

        public string VideoId { get; init; }

        public string UserId { get; init; }

        public string AuthorName { get; init; }

        public string Text { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
    }

    public static class CommentRules
    {
        public const int MaxLength = 500;

        public const int MaxNameLength = 40;

        /// <summary>A person cannot flood a video: at most this many comments in a minute</summary>
        public const int MaxPerMinute = 6;

        public const string UnknownName = "Someone";

        public static string CleanName(string? name)
        {
            var clean = string.IsNullOrWhiteSpace(name) ? UnknownName : name.Trim();
            return clean.Length > MaxNameLength ? clean[..MaxNameLength] : clean;
        }
    }

    public interface IVideoCommentRepo
    {
        Task Add(VideoComment comment);

        Task<VideoComment?> FindById(string commentId);

        /// <summary>The comments of a video, newest first; with <paramref name="before" />, the ones older than that</summary>
        Task<IReadOnlyList<VideoComment>> FindOfVideo(string videoId, DateTimeOffset? before, int limit);

        Task Delete(string commentId);

        Task DeleteOfVideo(string videoId);

        /// <summary>How many comments each video has (a video without any is not in the answer)</summary>
        Task<IReadOnlyDictionary<string, long>> CountsOf(IEnumerable<string> videoIds);

        Task<long> CountSince(string userId, DateTimeOffset since);
    }

    /// <summary>A person writes a comment under a video</summary>
    public record PostCommentRequest(string UserId, string AuthorName, string VideoId, string? Text)
        : IRequest<SuccessOrError<VideoComment>>;

    /// <summary>Takes a comment away: the one who wrote it, the creator of the video, or an administrator</summary>
    public record DeleteCommentRequest(string UserId, bool IsAdmin, string VideoId, string CommentId)
        : IRequest<SuccessOrError<Unit>>;

    public class PostCommentUseCase : IRequestHandler<PostCommentRequest, SuccessOrError<VideoComment>>
    {
        private readonly IVideoRepo _videos;
        private readonly IVideoCommentRepo _comments;
        private readonly VideoAccess _access;
        private readonly TimeProvider _timeProvider;

        public PostCommentUseCase(IVideoRepo videos, IVideoCommentRepo comments, VideoAccess access,
            TimeProvider timeProvider)
        {
            _videos = videos;
            _comments = comments;
            _access = access;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<VideoComment>> Handle(PostCommentRequest request,
            CancellationToken cancellationToken)
        {
            var video = await _videos.FindById(request.VideoId);
            if (video == null || !(video.IsPublic || video.CreatorUserId == request.UserId))
                return VideosError.NotFound;

            var text = request.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return SocialError.InvalidComment("Write something first.");
            if (text.Length > CommentRules.MaxLength)
                return SocialError.InvalidComment($"Keep it to {CommentRules.MaxLength} characters.");

            // the talk about a video that costs money is for the people who can watch it
            if (!await _access.CanWatch(video, request.UserId)) return SocialError.CannotComment;

            var now = _timeProvider.GetUtcNow();
            if (await _comments.CountSince(request.UserId, now.AddMinutes(-1)) >= CommentRules.MaxPerMinute)
                return SocialError.TooFast;

            var comment = new VideoComment(Guid.NewGuid().ToString("N"), video.VideoId, request.UserId,
                CommentRules.CleanName(request.AuthorName), text, now);
            await _comments.Add(comment);
            return comment;
        }
    }

    public class DeleteCommentUseCase : IRequestHandler<DeleteCommentRequest, SuccessOrError<Unit>>
    {
        private readonly IVideoRepo _videos;
        private readonly IVideoCommentRepo _comments;

        public DeleteCommentUseCase(IVideoRepo videos, IVideoCommentRepo comments)
        {
            _videos = videos;
            _comments = comments;
        }

        public async Task<SuccessOrError<Unit>> Handle(DeleteCommentRequest request,
            CancellationToken cancellationToken)
        {
            var comment = await _comments.FindById(request.CommentId);
            if (comment == null || comment.VideoId != request.VideoId) return SocialError.CommentNotFound;

            if (comment.UserId != request.UserId && !request.IsAdmin)
            {
                var video = await _videos.FindById(comment.VideoId);
                // somebody else's comment is not shown to be there
                if (video == null || video.CreatorUserId != request.UserId) return SocialError.CommentNotFound;
            }

            await _comments.Delete(comment.CommentId);
            return Unit.Value;
        }
    }
}
