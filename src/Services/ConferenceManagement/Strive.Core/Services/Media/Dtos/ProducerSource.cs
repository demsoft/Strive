using System.Runtime.Serialization;

namespace Strive.Core.Services.Media.Dtos
{
    public enum ProducerSource
    {
        Mic,
        Webcam,
        Screen,

        /// <summary>The sound of the shared screen (a tab or the system), next to the microphone</summary>
        [EnumMember(Value = "screen-audio")] ScreenAudio,

        [EnumMember(Value = "loopback-mic")] LoopbackMic,

        [EnumMember(Value = "loopback-webcam")]
        LoopbackWebcam,

        [EnumMember(Value = "loopback-screen")]
        LoopbackScreen,
    }
}
