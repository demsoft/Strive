using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Strive.Admin
{
    /// <summary>Writes the numbers of the overview to the history once per interval.</summary>
    public class AdminMetricsSampler : BackgroundService
    {
        private readonly ILogger<AdminMetricsSampler> _logger;
        private readonly AdminOptions _options;
        private readonly AdminOverviewService _overview;
        private readonly IAdminMetricsStore _store;

        public AdminMetricsSampler(AdminOverviewService overview, IAdminMetricsStore store,
            IOptions<AdminOptions> options, ILogger<AdminMetricsSampler> logger)
        {
            _overview = overview;
            _store = store;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.SamplingEnabled) return;

            // the other services need a moment after a start
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ContinueWith(_ => { });

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var overview = await _overview.GetOverviewAsync(stoppingToken);
                    await _store.AddAsync(AdminOverviewService.ToSample(overview, DateTime.UtcNow));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogWarning(e, "Could not write the admin metrics");
                }

                try
                {
                    await Task.Delay(_options.SampleInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
