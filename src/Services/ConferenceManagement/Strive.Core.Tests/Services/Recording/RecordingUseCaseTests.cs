using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Strive.Core.Domain.Entities;
using Strive.Core.Services;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.ConferenceManagement.Requests;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.NotificationHandlers;
using Strive.Core.Services.Recording.Requests;
using Strive.Core.Services.Recording.UseCases;
using Strive.Core.Services.Synchronization.Requests;
using Strive.Tests.Utils;
using Xunit;

namespace Strive.Core.Tests.Services.Recording
{
    public class RecordingUseCaseTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        private const string ConferenceId = "conference1";

        private readonly Participant _moderator = new(ConferenceId, "moderator1");
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IRecordingRepo> _repository = new();
        private readonly Mock<IRecorderClient> _recorder = new();
        private readonly Mock<IRecorderJoinTokenFactory> _tokenFactory = new();
        private readonly Mock<IRecordingStorage> _storage = new();
        private readonly RecordingOptions _options = new() {Enabled = true, RetentionDays = 30, MaxDurationMinutes = 120};

        private class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private static IOptions<RecordingOptions> Wrap(RecordingOptions options)
        {
            return Options.Create(options);
        }

        private void SetupConference(bool recordingEnabled = true)
        {
            var conference = new Conference(ConferenceId)
            {
                Configuration = new ConferenceConfiguration
                {
                    Recording = new ConferenceRecordingOptions {IsEnabled = recordingEnabled},
                },
            };
            _mediator.Setup(x => x.Send(It.IsAny<FindConferenceByIdRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(conference);
        }

        private ConferenceRecording CreateRecording(RecordingStatus status = RecordingStatus.Recording)
        {
            return new ConferenceRecording("rec1", ConferenceId, _moderator.Id, Now, Now.AddDays(30), "token")
            {
                Status = status,
            };
        }

        private StartRecordingUseCase CreateStart()
        {
            _repository.Setup(x => x.TryCreate(It.IsAny<ConferenceRecording>())).ReturnsAsync(true);
            _tokenFactory.Setup(x => x.Create(It.IsAny<string>(), ConferenceId, It.IsAny<TimeSpan>()))
                .Returns("join-token");
            return new StartRecordingUseCase(_mediator.Object, _repository.Object, _recorder.Object,
                _tokenFactory.Object, Wrap(_options), new FixedTimeProvider(),
                NullLogger<StartRecordingUseCase>.Instance);
        }

        // start

        [Fact]
        public async Task Start_FeatureDisabledOnServer_ReturnNotEnabled()
        {
            _options.Enabled = false;
            SetupConference();

            var result = await CreateStart().Handle(new StartRecordingRequest(_moderator), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Recording_NotEnabled), result.Error!.Code);
            _recorder.Verify(x => x.Start(It.IsAny<RecorderStartCommand>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Start_DisabledInConference_ReturnNotEnabled()
        {
            SetupConference(false);

            var result = await CreateStart().Handle(new StartRecordingRequest(_moderator), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Recording_NotEnabled), result.Error!.Code);
        }

        [Fact]
        public async Task Start_AlreadyRecording_ReturnErrorAndDoNotStartRecorder()
        {
            SetupConference();
            var useCase = CreateStart();
            _repository.Setup(x => x.TryCreate(It.IsAny<ConferenceRecording>())).ReturnsAsync(false);

            var result = await useCase.Handle(new StartRecordingRequest(_moderator), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Recording_AlreadyRecording), result.Error!.Code);
            _recorder.Verify(x => x.Start(It.IsAny<RecorderStartCommand>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Start_Success_StoreRecordingAndStartRecorder()
        {
            // arrange
            SetupConference();
            var useCase = CreateStart();
            ConferenceRecording? created = null;
            _repository.Setup(x => x.TryCreate(It.IsAny<ConferenceRecording>()))
                .Callback<ConferenceRecording>(x => created = x).ReturnsAsync(true);
            RecorderStartCommand? command = null;
            _recorder.Setup(x => x.Start(It.IsAny<RecorderStartCommand>(), It.IsAny<CancellationToken>()))
                .Callback<RecorderStartCommand, CancellationToken>((x, _) => command = x).Returns(Task.CompletedTask);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            // act
            var result = await useCase.Handle(new StartRecordingRequest(_moderator), default);

            // assert
            Assert.True(result.Success);
            Assert.NotNull(created);
            Assert.Equal(RecordingStatus.Starting, created!.Status);
            Assert.Equal(_moderator.Id, created.StartedBy);
            Assert.Equal(Now, created.StartedAt);
            Assert.Equal(Now.AddDays(30), created.ExpiresAt);
            Assert.Equal(RecordingVisibility.SignedIn, created.Visibility);
            Assert.False(string.IsNullOrWhiteSpace(created.ShareToken));

            Assert.NotNull(command);
            Assert.Equal(created.RecordingId, command!.RecordingId);
            Assert.Equal(ConferenceId, command.ConferenceId);
            Assert.Equal("join-token", command.JoinToken);
            Assert.Equal($"recordings/{ConferenceId}/{created.RecordingId}.mp4", command.StorageKey);
            Assert.Equal(120, command.MaxDurationMinutes);

            update.AssertReceived();
            Assert.Equal(SynchronizedRecording.SyncObjId, update.GetRequest().SynchronizedObjectId);
        }

        [Fact]
        public async Task Start_RecorderCannotBeStarted_MarkRecordingAsFailed()
        {
            // arrange
            SetupConference();
            var useCase = CreateStart();
            ConferenceRecording? created = null;
            _repository.Setup(x => x.TryCreate(It.IsAny<ConferenceRecording>()))
                .Callback<ConferenceRecording>(x => created = x).ReturnsAsync(true);
            _recorder.Setup(x => x.Start(It.IsAny<RecorderStartCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("recorder is down"));

            // act
            var result = await useCase.Handle(new StartRecordingRequest(_moderator), default);

            // assert
            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Recording_StartFailed), result.Error!.Code);
            Assert.Equal(RecordingStatus.Failed, created!.Status);
            Assert.NotNull(created.FailureReason);
            _repository.Verify(x => x.Update(created), Times.Once);
        }

        // stop

        private StopRecordingUseCase CreateStop()
        {
            return new StopRecordingUseCase(_mediator.Object, _repository.Object, _recorder.Object,
                new FixedTimeProvider(), NullLogger<StopRecordingUseCase>.Instance);
        }

        [Fact]
        public async Task Stop_NothingRecorded_ReturnError()
        {
            _repository.Setup(x => x.FindActiveOfConference(ConferenceId))
                .ReturnsAsync((ConferenceRecording?) null);

            var result = await CreateStop().Handle(new StopRecordingRequest(ConferenceId), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Recording_NotRecording), result.Error!.Code);
        }

        [Fact]
        public async Task Stop_Recording_FinalizeAndTellRecorder()
        {
            var recording = CreateRecording();
            _repository.Setup(x => x.FindActiveOfConference(ConferenceId)).ReturnsAsync(recording);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            var result = await CreateStop().Handle(new StopRecordingRequest(ConferenceId), default);

            Assert.True(result.Success);
            Assert.Equal(RecordingStatus.Finalizing, recording.Status);
            Assert.Equal(Now, recording.EndedAt);
            _repository.Verify(x => x.Update(recording), Times.Once);
            _recorder.Verify(x => x.Stop("rec1", It.IsAny<CancellationToken>()), Times.Once);
            update.AssertReceived();
        }

        [Fact]
        public async Task Stop_AlreadyFinalizing_OnlyTellRecorderAgain()
        {
            var recording = CreateRecording(RecordingStatus.Finalizing);
            _repository.Setup(x => x.FindActiveOfConference(ConferenceId)).ReturnsAsync(recording);

            var result = await CreateStop().Handle(new StopRecordingRequest(ConferenceId), default);

            Assert.True(result.Success);
            _repository.Verify(x => x.Update(It.IsAny<ConferenceRecording>()), Times.Never);
            _recorder.Verify(x => x.Stop("rec1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Stop_RecorderUnreachable_StillSucceedAndKeepFinalizing()
        {
            var recording = CreateRecording();
            _repository.Setup(x => x.FindActiveOfConference(ConferenceId)).ReturnsAsync(recording);
            _recorder.Setup(x => x.Stop("rec1", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException());

            var result = await CreateStop().Handle(new StopRecordingRequest(ConferenceId), default);

            Assert.True(result.Success);
            Assert.Equal(RecordingStatus.Finalizing, recording.Status);
        }

        // reports of the recorder

        private RecorderReportUseCase CreateReport()
        {
            return new RecorderReportUseCase(_mediator.Object, _repository.Object, new FixedTimeProvider());
        }

        [Fact]
        public async Task Report_UnknownRecording_ReturnNotFound()
        {
            _repository.Setup(x => x.FindById("x")).ReturnsAsync((ConferenceRecording?) null);

            var result = await CreateReport().Handle(
                new RecorderReportRequest("x", new RecorderReport(RecorderEvent.Started)), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Recording_NotFound), result.Error!.Code);
        }

        [Fact]
        public async Task Report_Started_RecordingStarts()
        {
            var recording = CreateRecording(RecordingStatus.Starting);
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(recording);
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            await CreateReport().Handle(new RecorderReportRequest("rec1", new RecorderReport(RecorderEvent.Started)),
                default);

            Assert.Equal(RecordingStatus.Recording, recording.Status);
            update.AssertReceived();
        }

        [Fact]
        public async Task Report_StartedAfterStop_KeepFinalizing()
        {
            var recording = CreateRecording(RecordingStatus.Finalizing);
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(recording);

            await CreateReport().Handle(new RecorderReportRequest("rec1", new RecorderReport(RecorderEvent.Started)),
                default);

            Assert.Equal(RecordingStatus.Finalizing, recording.Status);
        }

        [Fact]
        public async Task Report_Finished_StoreFileDetails()
        {
            var recording = CreateRecording(RecordingStatus.Finalizing);
            recording.EndedAt = Now.AddMinutes(-1);
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(recording);

            await CreateReport().Handle(new RecorderReportRequest("rec1",
                new RecorderReport(RecorderEvent.Finished)
                {
                    StorageKey = "recordings/conference1/rec1.mp4", SizeBytes = 1234, DurationSeconds = 61.5,
                }), default);

            Assert.Equal(RecordingStatus.Ready, recording.Status);
            Assert.Equal("recordings/conference1/rec1.mp4", recording.StorageKey);
            Assert.Equal(1234, recording.SizeBytes);
            Assert.Equal(61.5, recording.DurationSeconds);
            Assert.Equal(Now.AddMinutes(-1), recording.EndedAt);
            _repository.Verify(x => x.Update(recording), Times.Once);
        }

        [Fact]
        public async Task Report_Failed_StoreReason()
        {
            var recording = CreateRecording();
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(recording);

            await CreateReport().Handle(new RecorderReportRequest("rec1",
                new RecorderReport(RecorderEvent.Failed) {Reason = "ffmpeg crashed"}), default);

            Assert.Equal(RecordingStatus.Failed, recording.Status);
            Assert.Equal("ffmpeg crashed", recording.FailureReason);
            Assert.Equal(Now, recording.EndedAt);
        }

        [Theory]
        [InlineData(RecordingStatus.Ready)]
        [InlineData(RecordingStatus.Failed)]
        public async Task Report_RecordingAlreadyFinished_IgnoreLateReport(RecordingStatus status)
        {
            var recording = CreateRecording(status);
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(recording);

            var result = await CreateReport().Handle(new RecorderReportRequest("rec1",
                new RecorderReport(RecorderEvent.Failed) {Reason = "late"}), default);

            Assert.True(result.Success);
            Assert.Equal(status, recording.Status);
            _repository.Verify(x => x.Update(It.IsAny<ConferenceRecording>()), Times.Never);
        }

        // delete and visibility

        [Fact]
        public async Task Delete_ReadyRecording_DeleteFileAndRecord()
        {
            var recording = CreateRecording(RecordingStatus.Ready);
            recording.StorageKey = "key";
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(recording);

            var result = await new DeleteRecordingUseCase(_repository.Object, _storage.Object).Handle(
                new DeleteRecordingRequest("rec1"), default);

            Assert.True(result.Success);
            _storage.Verify(x => x.Delete("key", It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(x => x.Delete("rec1"), Times.Once);
        }

        [Theory]
        [InlineData(RecordingStatus.Starting)]
        [InlineData(RecordingStatus.Recording)]
        [InlineData(RecordingStatus.Finalizing)]
        public async Task Delete_RecordingInProgress_ReturnError(RecordingStatus status)
        {
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(CreateRecording(status));

            var result = await new DeleteRecordingUseCase(_repository.Object, _storage.Object).Handle(
                new DeleteRecordingRequest("rec1"), default);

            Assert.False(result.Success);
            _repository.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Delete_NotFound_ReturnError()
        {
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync((ConferenceRecording?) null);

            var result = await new DeleteRecordingUseCase(_repository.Object, _storage.Object).Handle(
                new DeleteRecordingRequest("rec1"), default);

            Assert.False(result.Success);
            Assert.Equal(nameof(ServiceErrorCode.Recording_NotFound), result.Error!.Code);
        }

        [Fact]
        public async Task SetVisibility_StoreVisibility()
        {
            var recording = CreateRecording(RecordingStatus.Ready);
            _repository.Setup(x => x.FindById("rec1")).ReturnsAsync(recording);

            var result = await new SetRecordingVisibilityUseCase(_repository.Object).Handle(
                new SetRecordingVisibilityRequest("rec1", RecordingVisibility.AnyoneWithLink), default);

            Assert.True(result.Success);
            Assert.Equal(RecordingVisibility.AnyoneWithLink, recording.Visibility);
            _repository.Verify(x => x.Update(recording), Times.Once);
        }

        // retention

        private ExpireRecordingsUseCase CreateExpire()
        {
            return new ExpireRecordingsUseCase(_mediator.Object, _repository.Object, _storage.Object,
                Wrap(_options), new FixedTimeProvider(), NullLogger<ExpireRecordingsUseCase>.Instance);
        }

        [Fact]
        public async Task Expire_ExpiredRecordings_DeleteFilesAndRecords()
        {
            var expired = CreateRecording(RecordingStatus.Ready);
            expired.StorageKey = "key";
            _repository.Setup(x => x.FindExpired(Now)).ReturnsAsync(new List<ConferenceRecording> {expired});
            _repository.Setup(x => x.FindUnfinishedStartedBefore(It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(new List<ConferenceRecording>());

            await CreateExpire().Handle(new ExpireRecordingsRequest(), default);

            _storage.Verify(x => x.Delete("key", It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(x => x.Delete("rec1"), Times.Once);
        }

        [Fact]
        public async Task Expire_StorageFails_KeepRecordForNextRun()
        {
            var expired = CreateRecording(RecordingStatus.Ready);
            expired.StorageKey = "key";
            _repository.Setup(x => x.FindExpired(Now)).ReturnsAsync(new List<ConferenceRecording> {expired});
            _repository.Setup(x => x.FindUnfinishedStartedBefore(It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(new List<ConferenceRecording>());
            _storage.Setup(x => x.Delete("key", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException());

            await CreateExpire().Handle(new ExpireRecordingsRequest(), default);

            _repository.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Expire_RecorderNeverReported_FailRecording()
        {
            var stuck = CreateRecording(RecordingStatus.Recording);
            _repository.Setup(x => x.FindExpired(Now)).ReturnsAsync(new List<ConferenceRecording>());
            // max duration 120 min + 30 min grace
            _repository.Setup(x => x.FindUnfinishedStartedBefore(Now.AddMinutes(-150)))
                .ReturnsAsync(new List<ConferenceRecording> {stuck});
            var update = _mediator.CaptureRequest<UpdateSynchronizedObjectRequest>();

            await CreateExpire().Handle(new ExpireRecordingsRequest(), default);

            Assert.Equal(RecordingStatus.Failed, stuck.Status);
            _repository.Verify(x => x.Update(stuck), Times.Once);
            update.AssertReceived();
        }

        // conference closed

        [Fact]
        public async Task ConferenceClosed_StopRecording()
        {
            var stop = _mediator.CaptureRequest<StopRecordingRequest, Strive.Core.Interfaces.SuccessOrError<Unit>>();

            await new ConferenceClosedNotificationHandler(_mediator.Object).Handle(
                new ConferenceClosedNotification(ConferenceId), default);

            stop.AssertReceived();
            Assert.Equal(ConferenceId, stop.GetRequest().ConferenceId);
        }

        [Fact]
        public void Tokens_AreUniqueAndUrlSafe()
        {
            var a = RecordingTokens.CreateShareToken();
            var b = RecordingTokens.CreateShareToken();

            Assert.NotEqual(a, b);
            Assert.Matches("^[A-Za-z0-9_-]+$", a);
            Assert.True(a.Length >= 32);
        }
    }
}
