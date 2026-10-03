using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;

namespace Strive.Core.Services.Recording.UseCases
{
    public class SetRecordingVisibilityUseCase : IRequestHandler<SetRecordingVisibilityRequest, SuccessOrError<Unit>>
    {
        private readonly IRecordingRepo _repository;

        public SetRecordingVisibilityUseCase(IRecordingRepo repository)
        {
            _repository = repository;
        }

        public async Task<SuccessOrError<Unit>> Handle(SetRecordingVisibilityRequest request,
            CancellationToken cancellationToken)
        {
            var recording = await _repository.FindById(request.RecordingId);
            if (recording == null) return RecordingError.RecordingNotFound;

            recording.Visibility = request.Visibility;
            await _repository.Update(recording);

            return Unit.Value;
        }
    }
}
