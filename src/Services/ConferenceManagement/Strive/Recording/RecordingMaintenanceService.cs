using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Requests;

namespace Strive.Recording
{
    /// <summary>
    ///     Deletes recordings after their retention time and fails recordings whose recorder disappeared
    /// </summary>
    public class RecordingMaintenanceService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

        private readonly ILifetimeScope _scope;
        private readonly RecordingOptions _options;
        private readonly ILogger<RecordingMaintenanceService> _logger;

        public RecordingMaintenanceService(ILifetimeScope scope, IOptions<RecordingOptions> options,
            ILogger<RecordingMaintenanceService> logger)
        {
            _scope = scope;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled) return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scope.BeginLifetimeScope();
                    await scope.Resolve<IMediator>().Send(new ExpireRecordingsRequest(), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Recording maintenance failed");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
