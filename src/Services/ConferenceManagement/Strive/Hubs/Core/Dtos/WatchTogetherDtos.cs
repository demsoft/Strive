using System.ComponentModel.DataAnnotations;
using Strive.Core.Services.WatchTogether.Requests;

namespace Strive.Hubs.Core.Dtos
{
    public record StartWatchTogetherDto([property: Required, MaxLength(2000)] string Url);

    public record ControlWatchTogetherDto(WatchTogetherAction Action, double? PositionSeconds, double? Rate);
}
