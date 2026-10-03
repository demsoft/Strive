using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Recording.UseCases
{
    public class RecorderReportUseCase : IRequestHandler<RecorderReportRequest, SuccessOrError<Unit>>
    {
        private readonly IMediator _mediator;
        private readonly IRecordingRepo _repository;
        private readonly TimeProvider _timeProvider;

        public RecorderReportUseCase(IMediator mediator, IRecordingRepo repository, TimeProvider timeProvider)
        {
            _mediator = mediator;
            _repository = repository;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<Unit>> Handle(RecorderReportRequest request,
            CancellationToken cancellationToken)
        {
            var (recordingId, report) = request;

            var recording = await _repository.FindById(recordingId);
            if (recording == null) return RecordingError.RecordingNotFound;

            // reports may arrive more than once or late, a finished recording never changes
            if (recording.Status is RecordingStatus.Ready or RecordingStatus.Failed) return Unit.Value;

            switch (report.Event)
            {
                case RecorderEvent.Started:
                    if (recording.Status == RecordingStatus.Starting) recording.Status = RecordingStatus.Recording;
                    break;
                case RecorderEvent.Finished:
                    recording.Status = RecordingStatus.Ready;
                    recording.StorageKey = report.StorageKey;
                    recording.SizeBytes = report.SizeBytes;
                    recording.DurationSeconds = report.DurationSeconds;
                    recording.EndedAt ??= _timeProvider.GetUtcNow();
                    break;
                case RecorderEvent.Failed:
                    recording.Status = RecordingStatus.Failed;
                    recording.FailureReason = report.Reason;
                    recording.EndedAt ??= _timeProvider.GetUtcNow();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request));
            }

            await _repository.Update(recording);
            await _mediator.Send(
                new UpdateSynchronizedObjectRequest(recording.ConferenceId, SynchronizedRecording.SyncObjId),
                cancellationToken);

            return Unit.Value;
        }
    }
}
