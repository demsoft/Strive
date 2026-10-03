using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Recording
{
    public class RecordingError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error NotEnabled =>
            BadRequest("Recording is not available for this conference.", ServiceErrorCode.Recording_NotEnabled);

        public static Error AlreadyRecording =>
            BadRequest("The conference is already being recorded.", ServiceErrorCode.Recording_AlreadyRecording);

        public static Error NotRecording =>
            BadRequest("The conference is not being recorded.", ServiceErrorCode.Recording_NotRecording);

        public static Error StartFailed =>
            BadRequest("The recorder could not be started.", ServiceErrorCode.Recording_StartFailed);

        public static Error RecordingNotFound =>
            NotFound("The recording was not found.", ServiceErrorCode.Recording_NotFound);
    }
}
