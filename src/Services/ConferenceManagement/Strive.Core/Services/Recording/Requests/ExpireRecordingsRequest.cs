using MediatR;

namespace Strive.Core.Services.Recording.Requests
{
    /// <summary>
    ///     Delete recordings that passed their retention time and fail recordings whose recorder disappeared
    /// </summary>
    public record ExpireRecordingsRequest : IRequest;
}
