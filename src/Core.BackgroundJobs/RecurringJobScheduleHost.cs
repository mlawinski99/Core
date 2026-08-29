using Hangfire;
using Microsoft.Extensions.Hosting;

namespace Core.BackgroundJobs;

public class RecurringJobScheduleHost(IRecurringJobManager recurringJobManager, Action<IRecurringJobManager> schedule)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        schedule(recurringJobManager);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
