using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Strive.Core.Interfaces;
using Strive.Core.Services.Media;
using Strive.Core.Services.Media.Dtos;
using Strive.Hubs.Core;
using Strive.IntegrationTests._Helpers;
using Strive.Messaging.SFU.Dto;
using Strive.Messaging.SFU.SendContracts;
using Strive.Tests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class MediaTests : ServiceIntegrationTest
    {
        public MediaTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper, mongoDb)
        {
        }

        private Mock<IPublishObserver> ConnectPublishObserver()
        {
            var mockObserver = new Mock<IPublishObserver>();
            var busControl = Factory.Services.GetRequiredService<IBusControl>();
            busControl.ConnectPublishObserver(mockObserver.Object);

            return mockObserver;
        }

        [Fact]
        public async Task FetchSfuConnectionInfo_ClosedConference_ReturnInfo()
        {
            // arrange
            var conference = await CreateConference();
            var connection = await ConnectUserToConference(Moderator, conference);

            // act
            var result =
                await connection.Hub.InvokeAsync<SuccessOrError<SfuConnectionInfo>>(
                    nameof(CoreHub.FetchSfuConnectionInfo));

            // assert
            AssertSuccess(result);
            Assert.NotNull(result.Response!.AuthToken);
            Assert.NotNull(result.Response!.Url);
        }

        [Fact]
        public async Task FetchSfuConnectionInfo_TurnConfigured_ReturnIceServerWithValidCredentials()
        {
            // arrange
            var conference = await CreateConference();
            var connection = await ConnectUserToConference(Moderator, conference);

            // act
            var result =
                await connection.Hub.InvokeAsync<SuccessOrError<SfuConnectionInfo>>(
                    nameof(CoreHub.FetchSfuConnectionInfo));

            // assert
            AssertSuccess(result);
            var iceServer = Assert.Single(result.Response!.IceServers);
            Assert.Equal(new[] {"turn:turn.example.com:3478?transport=udp", "turn:turn.example.com:3478?transport=tcp"},
                iceServer.Urls);

            // username: "{expiry}:{participant id}", valid for about a day
            var (expiry, participantId) = iceServer.Username!.Split(':') is [var e, var p] ? (long.Parse(e), p) : default;
            Assert.Equal(Moderator.Sub, participantId);
            var lifetime = DateTimeOffset.FromUnixTimeSeconds(expiry) - DateTimeOffset.UtcNow;
            Assert.InRange(lifetime, TimeSpan.FromHours(23), TimeSpan.FromHours(25));

            // the credential is what coturn computes from the shared secret
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes("integration-test-secret"));
            Assert.Equal(Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(iceServer.Username))),
                iceServer.Credential);
        }

        [Fact]
        public async Task ChangeParticipantProducer_SendMessageToSfu()
        {
            // arrange
            var conference = await CreateConference(Moderator);
            var connection = await ConnectUserToConference(Moderator, conference);

            var request = new ChangeParticipantProducerDto("123", ProducerSource.Mic, MediaStreamAction.Pause);

            var observer = ConnectPublishObserver();
            observer.Setup(x => x.PostPublish(It.IsAny<PublishContext<ChangeParticipantProducer>>())).Callback(
                (PublishContext<ChangeParticipantProducer> context) =>
                {
                    var dto = context.Message;
                    Assert.Equal(conference.ConferenceId, dto.ConferenceId);
                    Assert.Equal(request.ParticipantId, dto.Payload.ParticipantId);
                    Assert.Equal(request.Source, dto.Payload.Source);
                    Assert.Equal(request.Action, dto.Payload.Action);
                });

            // act
            var result =
                await connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.ChangeParticipantProducer),
                    request);

            // assert
            AssertSuccess(result);

            await AssertHelper.WaitForAssert(() =>
            {
                observer.Verify(x => x.PostPublish(It.IsAny<PublishContext<ChangeParticipantProducer>>()),
                    Times.Once);
            });
        }
    }
}
