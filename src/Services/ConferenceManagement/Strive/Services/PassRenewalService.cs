using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Strive.Core.Services.Payments.Requests;

namespace Strive.Services
{
    /// <summary>Every ten minutes: charge the passes that renew themselves and are due, and tell people about coming renewals.</summary>
    public class PassRenewalService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

        private readonly ILifetimeScope _scope;
        private readonly ILogger<PassRenewalService> _logger;

        public PassRenewalService(ILifetimeScope scope, ILogger<PassRenewalService> logger)
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
                    var result = await scope.Resolve<IMediator>()
                        .Send(new RenewPassSubscriptionsRequest(), stoppingToken);
                    if (result.Success && result.Response is {Renewed: > 0} or {Failed: > 0} or {Ended: > 0})
                        _logger.LogInformation("Pass renewals: {renewed} renewed, {failed} failed, {ended} ended",
                            result.Response!.Renewed, result.Response.Failed, result.Response.Ended);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Renewing the passes failed");
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
