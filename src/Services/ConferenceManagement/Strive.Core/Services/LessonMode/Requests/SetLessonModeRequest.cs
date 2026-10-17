using MediatR;

namespace Strive.Core.Services.LessonMode.Requests
{
    public record SetLessonModeRequest(string ConferenceId, bool Active) : IRequest;
}
