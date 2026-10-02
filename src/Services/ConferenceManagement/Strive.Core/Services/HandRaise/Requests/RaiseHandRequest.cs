using MediatR;

namespace Strive.Core.Services.HandRaise.Requests
{
    public record RaiseHandRequest(Participant Participant) : IRequest;
}
