using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Strive.Admin;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Controllers
{
    /// <summary>The admin overview: who may open it, and what it tells.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class AdminTests
    {
        private class FakeSources : IAdminSourceClient
        {
            public Fetched<SfuStats>? Sfu { get; set; }
            public Fetched<AccountStats>? Accounts { get; set; }
            public Fetched<RecorderStats>? Recorder { get; set; }

            public Task<Fetched<SfuStats>?> GetSfuAsync(CancellationToken cancellationToken) => Task.FromResult(Sfu);

            public Task<Fetched<AccountStats>?> GetAccountsAsync(CancellationToken cancellationToken) =>
                Task.FromResult(Accounts);

            public Task<Fetched<RecorderStats>?> GetRecorderAsync(CancellationToken cancellationToken) =>
                Task.FromResult(Recorder);
        }

        private class FakeHistory : IAdminMetricsStore
        {
            public List<AdminSample> Samples { get; } = new();
            public DateTime? RequestedSince { get; private set; }

            public Task AddAsync(AdminSample sample)
            {
                Samples.Add(sample);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<AdminSample>> GetAsync(DateTime since, int maxPoints)
            {
                RequestedSince = since;
                return Task.FromResult<IReadOnlyList<AdminSample>>(Samples.Where(x => x.T >= since).ToList());
            }
        }

        private readonly FakeSources _sources = new();
        private readonly FakeHistory _history = new();

        private readonly MongoDbFixture _mongoDb;
        private readonly ITestOutputHelper _output;

        public AdminTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb)
        {
            _output = testOutputHelper;
            _mongoDb = mongoDb;
        }

        private HttpClient CreateAdminClient(string? role, out Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Startup> host)
        {
            var original = new CustomWebApplicationFactory(_mongoDb, _output);
            // the derived factory is the host that runs: its services are the ones to ask
            host = original.WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.AddSingleton<IAdminSourceClient>(_sources);
                services.AddSingleton<IAdminMetricsStore>(_history);
            }));
            var client = host.CreateClient();
            var factory = original;

            if (role != null)
            {
                var claims = new List<Claim> {new(ClaimTypes.NameIdentifier, "u1"), new("name", "Someone"), new("role", role)};
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.JwtTokens.GenerateJwtToken(claims));
            }

            return client;
        }

        private static SfuStats BusySfu() => new()
        {
            Workers = new List<SfuWorker> {new() {Index = 0, CpuPercent = 95}, new() {Index = 1, CpuPercent = 10}},
            Conferences = new List<SfuConference> {new() {ConferenceId = "c1", Participants = 7, Connections = 8, Producers = 14, Consumers = 90}},
            Totals = new SfuTotals {Conferences = 1, Participants = 7},
            System = new SfuSystem {CpuCores = 8, LoadAverage1m = 2, MemoryTotalBytes = 16_000_000_000, MemoryAvailableBytes = 8_000_000_000},
        };

        [Fact]
        public async Task Overview_NoToken_IsUnauthorized()
        {
            var client = CreateAdminClient(null, out _);

            var response = await client.GetAsync("/v1/admin/overview");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("mod")]
        [InlineData("admin")]
        [InlineData("serveradmin2")]
        [InlineData("")]
        public async Task Overview_OtherRolesAreForbidden(string role)
        {
            var client = CreateAdminClient(role, out _);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/v1/admin/overview")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/v1/admin/history")).StatusCode);
        }

        [Fact]
        public async Task Overview_ServerAdmin_SeesTheServerAndAWarningWhenItIsFull()
        {
            _sources.Sfu = new Fetched<SfuStats>(BusySfu(), null, 12);
            _sources.Accounts = new Fetched<AccountStats>(new AccountStats {Available = true, TotalUsers = 42}, null, 8);
            _sources.Recorder = new Fetched<RecorderStats>(new RecorderStats {Active = 1, MaxConcurrent = 2}, null, 5);
            var client = CreateAdminClient("serveradmin", out _);

            var response = await client.GetAsync("/v1/admin/overview");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("critical", json.GetProperty("status").GetString());
            Assert.Equal("full", json.GetProperty("capacity").GetProperty("level").GetString());
            Assert.Equal(95, json.GetProperty("capacity").GetProperty("maxWorkerCpuPercent").GetDouble());
            Assert.Equal(42, json.GetProperty("accounts").GetProperty("totalUsers").GetInt32());
            Assert.Equal(1, json.GetProperty("recorder").GetProperty("active").GetInt32());
            var services = json.GetProperty("services").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
            Assert.Contains("sfu", services);
            Assert.Contains("identity", services);
            Assert.Contains("recorder", services);
            Assert.Contains("mongodb", services);
            Assert.Equal(0, json.GetProperty("conferences").GetProperty("open").GetInt32());
        }

        [Fact]
        public async Task Overview_MediaServerDown_IsCritical()
        {
            _sources.Sfu = new Fetched<SfuStats>(null, "no answer within 4 seconds", 4000);
            var client = CreateAdminClient("serveradmin", out _);

            var json = await (await client.GetAsync("/v1/admin/overview")).Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("critical", json.GetProperty("status").GetString());
            Assert.Contains(json.GetProperty("services").EnumerateArray(),
                x => x.GetProperty("name").GetString() == "sfu" && x.GetProperty("status").GetString() == "down");
        }

        [Fact]
        public async Task Overview_ListsTheConferencesPeopleAreIn()
        {
            const string conference = "conference-abc";
            var client = CreateAdminClient("serveradmin", out var adminFactory);
            var tracker = adminFactory.Services.GetRequiredService<ActiveConferenceTracker>();
            tracker.ConferenceOpened(conference);
            tracker.ParticipantJoined(conference, "p1");
            tracker.ParticipantJoined(conference, "p2");

            var json = await (await client.GetAsync("/v1/admin/overview")).Content.ReadFromJsonAsync<JsonElement>();

            var conferences = json.GetProperty("conferences");
            Assert.Equal(1, conferences.GetProperty("open").GetInt32());
            Assert.Equal(2, conferences.GetProperty("participants").GetInt32());
            Assert.Equal(conference, conferences.GetProperty("list")[0].GetProperty("conferenceId").GetString());
        }

        [Fact]
        public async Task History_ReturnsTheSamplesOfTheRequestedHours()
        {
            _history.Samples.Add(new AdminSample {T = DateTime.UtcNow.AddHours(-30), Participants = 1});
            _history.Samples.Add(new AdminSample {T = DateTime.UtcNow.AddHours(-2), Participants = 5, MaxWorkerCpu = 40});
            var client = CreateAdminClient("serveradmin", out _);

            var json = await (await client.GetAsync("/v1/admin/history?hours=24")).Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(24, json.GetProperty("hours").GetInt32());
            var samples = json.GetProperty("samples");
            Assert.Equal(1, samples.GetArrayLength());
            Assert.Equal(5, samples[0].GetProperty("participants").GetInt32());
            Assert.Equal(40, samples[0].GetProperty("maxWorkerCpu").GetDouble());
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(100000, 168)]
        public async Task History_HoursAreLimited(int requested, int expected)
        {
            var client = CreateAdminClient("serveradmin", out _);

            var json = await (await client.GetAsync($"/v1/admin/history?hours={requested}")).Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(expected, json.GetProperty("hours").GetInt32());
        }
    }
}
