using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Reactions
{
    public class ReactionError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error InvalidEmoji =>
            BadRequest("The emoji is not an allowed reaction.", ServiceErrorCode.Reactions_InvalidEmoji);

        public static Error RateLimited =>
            BadRequest("Too many reactions, slow down.", ServiceErrorCode.Reactions_RateLimited);
    }
}
