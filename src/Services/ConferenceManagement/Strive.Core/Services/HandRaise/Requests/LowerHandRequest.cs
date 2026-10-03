using MediatR;

namespace Strive.Core.Services.HandRaise.Requests
{
    public record LowerHandRequest(Participant Participant) : IRequest;
}
