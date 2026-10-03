using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Strive.Core.Interfaces;
using Strive.Core.Services;
using Strive.Core.Services.HandRaise;
using Strive.Hubs.Core;
using Strive.Hubs.Core.Dtos;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class HandRaiseTests : ServiceIntegrationTest
    {
        public HandRaiseTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private static Task<SuccessOrError<Unit>> Raise(UserConnection connection)
        {
            return connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.RaiseHand));
        }

        private static Task<SuccessOrError<Unit>> Lower(UserConnection connection)
        {
            return connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.LowerHand));
        }

        [Fact]
        public async Task RaiseHand_ConferenceOpen_SynchronizedObjectContainsParticipant()
        {
            // arrange
            var (connection, conference) = await ConnectToOpenedConference();
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);

            // act
            AssertSuccess(await Raise(connection));

            // assert
            await connection.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands =>
                {
                    var raised = Assert.Single(hands.Raised);
                    Assert.Equal(connection.User.Sub, raised.Key);
                    Assert.InRange(raised.Value, before, DateTimeOffset.UtcNow.AddSeconds(5));
                });
        }

        [Fact]
        public async Task RaiseHand_OtherParticipantIsConnected_OtherParticipantSeesTheHand()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);

            // act
            AssertSuccess(await Raise(userConnection));

            // assert
            await moderator.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands => Assert.Equal(user.Sub, Assert.Single(hands.Raised).Key));
        }

        [Fact]
        public async Task RaiseHand_RaisedTwice_KeepTheFirstTime()
        {
            // arrange
            var (connection, _) = await ConnectToOpenedConference();
            AssertSuccess(await Raise(connection));
            var first = (await connection.SyncObjects.WaitForSyncObj<SynchronizedHandRaises>(
                SynchronizedHandRaises.SyncObjId)).Raised[connection.User.Sub];

            await Task.Delay(50);

            // act
            AssertSuccess(await Raise(connection));

            // assert
            await connection.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands => Assert.Equal(first, Assert.Single(hands.Raised).Value));
        }

        [Fact]
        public async Task RaiseHand_TwoParticipants_OrderedByTime()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);

            // act
            AssertSuccess(await Raise(userConnection));
            await Task.Delay(50);
            AssertSuccess(await Raise(moderator));

            // assert
            await moderator.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands =>
                {
                    Assert.Equal(2, hands.Raised.Count);
                    Assert.Equal(new[] {user.Sub, moderator.User.Sub},
                        hands.Raised.OrderBy(x => x.Value).Select(x => x.Key));
                });
        }

        [Fact]
        public async Task RaiseHand_ConferenceNotOpen_ReturnError()
        {
            // arrange
            var conference = await CreateConference(Moderator);
            var connection = await ConnectUserToConference(Moderator, conference);

            // act
            var result = await Raise(connection);

            // assert
            AssertFailed(result);
        }

        [Fact]
        public async Task LowerHand_HandRaised_RemoveFromSynchronizedObject()
        {
            // arrange
            var (connection, _) = await ConnectToOpenedConference();
            AssertSuccess(await Raise(connection));
            await connection.SyncObjects.WaitForSyncObj<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId);

            // act
            AssertSuccess(await Lower(connection));

            // assert
            await connection.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands => Assert.Empty(hands.Raised));
        }

        [Fact]
        public async Task LowerHand_HandNotRaised_Succeed()
        {
            var (connection, _) = await ConnectToOpenedConference();

            AssertSuccess(await Lower(connection));
        }

        [Fact]
        public async Task LowerParticipantsHand_ModeratorLowersHandOfUser_RemoveFromSynchronizedObject()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);
            AssertSuccess(await Raise(userConnection));
            await moderator.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands => Assert.Single(hands.Raised));

            // act
            var result = await moderator.Hub.InvokeAsync<SuccessOrError<Unit>>(
                nameof(CoreHub.LowerParticipantsHand), new LowerParticipantsHandDto(user.Sub));

            // assert
            AssertSuccess(result);
            await userConnection.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(
                SynchronizedHandRaises.SyncObjId, hands => Assert.Empty(hands.Raised));
        }

        [Fact]
        public async Task LowerParticipantsHand_UserWithoutPermission_ReturnPermissionDenied()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);
            AssertSuccess(await Raise(moderator));
            await userConnection.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(
                SynchronizedHandRaises.SyncObjId, hands => Assert.Single(hands.Raised));

            // act
            var result = await userConnection.Hub.InvokeAsync<SuccessOrError<Unit>>(
                nameof(CoreHub.LowerParticipantsHand), new LowerParticipantsHandDto(moderator.User.Sub));

            // assert
            AssertFailed(result);
            AssertErrorCode(ServiceErrorCode.PermissionDenied, result.Error!);
            await Task.Delay(200);
            Assert.Single(userConnection.SyncObjects.GetSynchronizedObject<SynchronizedHandRaises>(
                SynchronizedHandRaises.SyncObjId).Raised);
        }

        [Fact]
        public async Task Leave_HandRaised_RemoveFromSynchronizedObject()
        {
            // arrange
            var (moderator, conference) = await ConnectToOpenedConference();
            var user = CreateUser();
            var userConnection = await ConnectUserToConference(user, conference);
            AssertSuccess(await Raise(userConnection));
            await moderator.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands => Assert.Single(hands.Raised));

            // act
            await userConnection.Hub.DisposeAsync();

            // assert
            await moderator.SyncObjects.AssertSyncObject<SynchronizedHandRaises>(SynchronizedHandRaises.SyncObjId,
                hands => Assert.Empty(hands.Raised));
        }
    }
}
