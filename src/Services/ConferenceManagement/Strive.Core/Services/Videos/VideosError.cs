using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Videos
{
    public class VideosError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error NotEnabled =>
            BadRequest("Videos are not available yet.", ServiceErrorCode.Videos_NotEnabled);

        public static Error Invalid(string message) => BadRequest(message, ServiceErrorCode.Videos_Invalid);

        public static Error NotFound => NotFound("This video was not found.", ServiceErrorCode.Videos_NotFound);

        public static Error HasBuyers =>
            BadRequest("People bought this video, so it cannot be deleted. Make it free instead if you do not want to sell it any more.",
                ServiceErrorCode.Videos_HasBuyers);

        public static Error NotForSale =>
            BadRequest("This video is not for sale.", ServiceErrorCode.Videos_NotForSale);

        public static Error UploadNotFound =>
            BadRequest("The video has not arrived yet. Wait until the upload is finished.",
                ServiceErrorCode.Videos_UploadNotFound);
    }
}
