using System.ComponentModel.DataAnnotations;
using Strive.Core.Services.WatchTogether.Requests;

namespace Strive.Hubs.Core.Dtos
{
    public record SetLessonModeDto(bool Active);

    public record ReportLatencyDto(double RttMs, double JitterBufferMs, double OutputMs);

    public record StartMetronomeDto([property: Range(30, 300)] int Bpm, [property: Range(1, 12)] int BeatsPerBar);

    public record StartWatchTogetherDto([property: Required, MaxLength(2000)] string Url);

    public record ControlWatchTogetherDto(WatchTogetherAction Action, double? PositionSeconds, double? Rate);
}
