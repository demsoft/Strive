using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Social
{
    public class SocialError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error CreatorNotFound =>
            NotFound("This creator was not found.", ServiceErrorCode.Social_NotFound);

        public static Error InvalidComment(string message) => BadRequest(message, ServiceErrorCode.Social_InvalidComment);

        public static Error CannotComment =>
            BadRequest("Buy this video to join the talk about it.", ServiceErrorCode.Social_CannotComment);

        public static Error TooFast =>
            BadRequest("Slow down a little and try again in a minute.", ServiceErrorCode.Social_TooFast);

        public static Error CommentNotFound =>
            NotFound("This comment was not found.", ServiceErrorCode.Social_CommentNotFound);

        public static Error OwnProfile =>
            BadRequest("You cannot follow yourself.", ServiceErrorCode.Social_Invalid);
    }
}
