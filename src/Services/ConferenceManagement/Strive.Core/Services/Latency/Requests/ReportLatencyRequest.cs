using MediatR;

namespace Strive.Core.Services.Latency.Requests
{
    public record ReportLatencyRequest(Participant Participant, LatencyReport Report) : IRequest;
}
