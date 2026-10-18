using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Strive.Core.Interfaces;
using Strive.Core.Services.Latency;
using Strive.Hubs.Core;
using Strive.Hubs.Core.Dtos;
using Strive.IntegrationTests._Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class LatencyTests : ServiceIntegrationTest
    {
        public LatencyTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private static Task<SuccessOrError<Unit>> Report(UserConnection connection, double rtt, double jitter,
            double output) =>
            connection.Hub.InvokeAsync<SuccessOrError<Unit>>(nameof(CoreHub.ReportLatency),
                new ReportLatencyDto(rtt, jitter, output));

        [Fact]
        public async Task Report_Musician_EveryoneSeesTheNumbers()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);

            AssertSuccess(await Report(student, 24, 35, 12));

            await moderator.SyncObjects.AssertSyncObject<SynchronizedLatency>(SynchronizedLatency.SyncObjId, value =>
            {
                var report = value.Reports[student.User.Sub];
                Assert.Equal(24, report.RttMs);
                Assert.Equal(35, report.JitterBufferMs);
                Assert.Equal(12, report.OutputMs);
            });
        }

        [Fact]
        public async Task Report_ImplausibleNumbers_NotStored()
        {
            var (moderator, conference) = await ConnectToOpenedConference();
            var student = await ConnectUserToConference(CreateUser(), conference);

            AssertSuccess(await Report(student, -5, 0, 0));

            await moderator.SyncObjects.AssertSyncObject<SynchronizedLatency>(SynchronizedLatency.SyncObjId,
                value => Assert.Empty(value.Reports));
        }
    }
}
