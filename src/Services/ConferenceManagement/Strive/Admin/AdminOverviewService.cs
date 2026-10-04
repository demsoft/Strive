using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.ConferenceManagement.Gateways;

namespace Strive.Admin
{
    /// <summary>Puts the numbers of all services together into the overview of the administrator.</summary>
    public class AdminOverviewService
    {
        private readonly ActiveConferenceTracker _conferences;
        private readonly IConferenceRepo _conferenceRepo;
        private readonly HealthCheckService _health;
        private readonly AdminOptions _options;
        private readonly IAdminSourceClient _sources;
        private readonly TimeProvider _timeProvider;

        public AdminOverviewService(IAdminSourceClient sources, ActiveConferenceTracker conferences,
            IConferenceRepo conferenceRepo, HealthCheckService health, IOptions<AdminOptions> options,
            TimeProvider timeProvider)
        {
            _sources = sources;
            _conferences = conferences;
            _conferenceRepo = conferenceRepo;
            _health = health;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task<AdminOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
        {
            var sfuTask = _sources.GetSfuAsync(cancellationToken);
            var accountsTask = _sources.GetAccountsAsync(cancellationToken);
            var recorderTask = _sources.GetRecorderAsync(cancellationToken);
            var healthTask = _health.CheckHealthAsync(cancellationToken);
            await Task.WhenAll(sfuTask, accountsTask, recorderTask, healthTask);

            var sfu = sfuTask.Result;
            var accounts = accountsTask.Result;
            var recorder = recorderTask.Result;

            var services = new List<ServiceStatus>();
            foreach (var (name, entry) in healthTask.Result.Entries)
                services.Add(new ServiceStatus(Normalize(name),
                    entry.Status switch
                    {
                        HealthStatus.Healthy => "up", HealthStatus.Degraded => "degraded", _ => "down",
                    },
                    entry.Status == HealthStatus.Healthy ? null : entry.Description ?? entry.Exception?.Message,
                    (int) entry.Duration.TotalMilliseconds));
            AddSource(services, "sfu", sfu);
            AddSource(services, "identity", accounts);
            AddSource(services, "recorder", recorder);

            // the conferences that people are in now, with the names and the numbers of the media server
            var tracked = _conferences.Snapshot();
            var sfuByConference = sfu?.Value?.Conferences.ToDictionary(x => x.ConferenceId) ?? new();
            var list = new List<ConferenceSummary>();
            foreach (var (conferenceId, participants, openedAt) in tracked)
            {
                var name = (await _conferenceRepo.FindById(conferenceId))?.Configuration.Name;
                sfuByConference.TryGetValue(conferenceId, out var media);
                list.Add(new ConferenceSummary(conferenceId, name, participants, openedAt, media?.Connections,
                    media?.Producers, media?.Consumers));
            }

            var totalParticipants = list.Sum(x => x.Participants);
            var (status, alerts, capacity) = AdminStatusEvaluator.Evaluate(sfu?.Value,
                !string.IsNullOrWhiteSpace(_options.SfuUrl), services, recorder?.Value, totalParticipants,
                _options.Thresholds);

            return new AdminOverview(_timeProvider.GetUtcNow(), status, alerts, capacity,
                new ConferencesInfo(list.Count, totalParticipants, list), sfu?.Value, services,
                accounts?.Value is {Available: true} ? accounts.Value : null, recorder?.Value, _options.Thresholds);
        }

        private static void AddSource<T>(List<ServiceStatus> services, string name, Fetched<T>? fetched)
            where T : class
        {
            if (fetched == null) return;
            services.Add(new ServiceStatus(name, fetched.Ok ? "up" : "down", fetched.Error, fetched.LatencyMs));
        }

        /// <summary>The names of the health checks are written for developers: "mongodb", "rabbitmq".</summary>
        private static string Normalize(string name) => name.Trim().ToLowerInvariant();

        public static AdminSample ToSample(AdminOverview overview, DateTime now) => new()
        {
            T = now,
            Participants = overview.Conferences.Participants,
            Conferences = overview.Conferences.Open,
            MaxWorkerCpu = overview.Capacity.MaxWorkerCpuPercent,
            AverageWorkerCpu = overview.Capacity.AverageWorkerCpuPercent,
            MemoryUsedPercent = overview.Capacity.MemoryUsedPercent,
            LoadPerCore = overview.Capacity.LoadPerCore,
            Alerts = overview.Alerts.Count,
        };
    }
}
