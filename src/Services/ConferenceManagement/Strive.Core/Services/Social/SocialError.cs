using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Social
{
    public class SocialError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error CreatorNotFound =>
            NotFound("This creator was not found.", ServiceErrorCode.Social_NotFound);

        public static Error OwnProfile =>
            BadRequest("You cannot follow yourself.", ServiceErrorCode.Social_Invalid);
    }
}
