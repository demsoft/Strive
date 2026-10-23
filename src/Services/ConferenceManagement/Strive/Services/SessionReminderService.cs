using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Strive.Core.Services.Reminders;

namespace Strive.Services
{
    /// <summary>Every few minutes: remind the ticket holders of the sessions that start soon.</summary>
    public class SessionReminderService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

        private readonly ILifetimeScope _scope;
        private readonly ILogger<SessionReminderService> _logger;

        public SessionReminderService(ILifetimeScope scope, ILogger<SessionReminderService> logger)
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
                    var result = await scope.Resolve<IMediator>().Send(new SendSessionRemindersRequest(), stoppingToken);
                    if (result.Success && result.Response > 0)
                        _logger.LogInformation("Sent {count} session reminders", result.Response);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Sending the session reminders failed");
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
