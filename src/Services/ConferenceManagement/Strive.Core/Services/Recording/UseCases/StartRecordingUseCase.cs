using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Strive.Core.Interfaces;
using Strive.Core.Services.ConferenceManagement.Requests;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Recording.UseCases
{
    public class StartRecordingUseCase : IRequestHandler<StartRecordingRequest, SuccessOrError<Unit>>
    {
        private readonly IMediator _mediator;
        private readonly IRecordingRepo _repository;
        private readonly IRecorderClient _recorder;
        private readonly IRecorderJoinTokenFactory _tokenFactory;
        private readonly RecordingOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StartRecordingUseCase> _logger;

        public StartRecordingUseCase(IMediator mediator, IRecordingRepo repository, IRecorderClient recorder,
            IRecorderJoinTokenFactory tokenFactory, IOptions<RecordingOptions> options, TimeProvider timeProvider,
            ILogger<StartRecordingUseCase> logger)
        {
            _mediator = mediator;
            _repository = repository;
            _recorder = recorder;
            _tokenFactory = tokenFactory;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<Unit>> Handle(StartRecordingRequest request,
            CancellationToken cancellationToken)
        {
            var (conferenceId, participantId) = request.Participant;

            var conference = await _mediator.Send(new FindConferenceByIdRequest(conferenceId), cancellationToken);
            if (!_options.Enabled || !conference.Configuration.Recording.IsEnabled) return RecordingError.NotEnabled;

            var now = _timeProvider.GetUtcNow();
            var recordingId = RecordingTokens.CreateRecordingId();
            var recording = new ConferenceRecording(recordingId, conferenceId, participantId, now,
                now.AddDays(_options.RetentionDays), RecordingTokens.CreateShareToken());

            // the repository refuses a second unfinished recording of the same conference, this also covers two
            // moderators pressing the button at the same time
            if (!await _repository.TryCreate(recording)) return RecordingError.AlreadyRecording;

            await UpdateSyncObject(conferenceId, cancellationToken);

            var storageKey = RecordingTokens.BuildStorageKey(conferenceId, recordingId);
            var maxDuration = TimeSpan.FromMinutes(_options.MaxDurationMinutes);
            var token = _tokenFactory.Create(recordingId, conferenceId, maxDuration + TimeSpan.FromMinutes(15));

            try
            {
                await _recorder.Start(
                    new RecorderStartCommand(recordingId, conferenceId, token, storageKey,
                        _options.MaxDurationMinutes), cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "The recorder could not be started for recording {recordingId}", recordingId);

                recording.Status = RecordingStatus.Failed;
                recording.FailureReason = "The recorder could not be started.";
                recording.EndedAt = _timeProvider.GetUtcNow();
                await _repository.Update(recording);
                await UpdateSyncObject(conferenceId, cancellationToken);

                return RecordingError.StartFailed;
            }

            return Unit.Value;
        }

        private async Task UpdateSyncObject(string conferenceId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new UpdateSynchronizedObjectRequest(conferenceId, SynchronizedRecording.SyncObjId),
                cancellationToken);
        }
    }
}
