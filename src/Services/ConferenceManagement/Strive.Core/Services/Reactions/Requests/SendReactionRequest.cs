using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Reactions.Requests
{
    public record SendReactionRequest(Participant Participant, string Emoji) : IRequest<SuccessOrError<Unit>>;
}
