using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.WatchTogether
{
    public class WatchTogetherError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error InvalidVideoUrl =>
            BadRequest("This is not a link to a YouTube video.", ServiceErrorCode.WatchTogether_InvalidVideoUrl);

        public static Error NothingPlaying =>
            BadRequest("There is no video to control.", ServiceErrorCode.WatchTogether_NothingPlaying);

        public static Error InvalidControl =>
            BadRequest("The position or the speed is not valid.", ServiceErrorCode.WatchTogether_InvalidControl);
    }
}
