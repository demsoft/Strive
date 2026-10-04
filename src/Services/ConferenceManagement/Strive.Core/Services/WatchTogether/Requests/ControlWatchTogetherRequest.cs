using MediatR;

namespace Strive.Core.Services.WatchTogether.Requests
{
    public enum WatchTogetherAction
    {
        Play,
        Pause,
        Seek,
        SetRate,
    }

    /// <param name="PositionSeconds">Where the video is at the host (Play, Pause, Seek)</param>
    /// <param name="Rate">The new speed (SetRate)</param>
    public record ControlWatchTogetherRequest(string ConferenceId, WatchTogetherAction Action, double? PositionSeconds,
        double? Rate) : IRequest;
}
