using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Strive.Core.Services.Videos;

namespace Strive.Services
{
    /// <summary>Every half minute: copy the videos that Cloudinary finished to the storage, give up on the ones that never do.</summary>
    public class VideoProcessingService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        private readonly ILifetimeScope _scope;
        private readonly ILogger<VideoProcessingService> _logger;

        public VideoProcessingService(ILifetimeScope scope, ILogger<VideoProcessingService> logger)
        {
            _scope = scope;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scope.BeginLifetimeScope();
                    var result = await scope.Resolve<IMediator>().Send(new ProcessVideosRequest(), stoppingToken);
                    if (result.Success && result.Response > 0)
                        _logger.LogInformation("{count} videos are ready", result.Response);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Processing the videos failed");
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
