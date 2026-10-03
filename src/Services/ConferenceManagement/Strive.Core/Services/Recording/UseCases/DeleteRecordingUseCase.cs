using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;

namespace Strive.Core.Services.Recording.UseCases
{
    public class DeleteRecordingUseCase : IRequestHandler<DeleteRecordingRequest, SuccessOrError<Unit>>
    {
        private readonly IRecordingRepo _repository;
        private readonly IRecordingStorage _storage;

        public DeleteRecordingUseCase(IRecordingRepo repository, IRecordingStorage storage)
        {
            _repository = repository;
            _storage = storage;
        }

        public async Task<SuccessOrError<Unit>> Handle(DeleteRecordingRequest request,
            CancellationToken cancellationToken)
        {
            var recording = await _repository.FindById(request.RecordingId);
            if (recording == null) return RecordingError.RecordingNotFound;

            // a running recording must be stopped first, otherwise the recorder would upload into the void
            if (recording.Status is RecordingStatus.Starting or RecordingStatus.Recording
                or RecordingStatus.Finalizing)
                return RecordingError.AlreadyRecording;

            if (recording.StorageKey != null) await _storage.Delete(recording.StorageKey, cancellationToken);
            await _repository.Delete(recording.RecordingId);

            return Unit.Value;
        }
    }
}
