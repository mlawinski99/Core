using Hangfire;

namespace Core.BackgroundJobs;

public class HangfireRecurringJobScheduler : IRecurringJobScheduler
{
    private readonly IRecurringJobManager _recurringJobManager;

    public HangfireRecurringJobScheduler(IRecurringJobManager recurringJobManager)
    {
        _recurringJobManager = recurringJobManager;
    }

    public void ScheduleRecurring<TJob>(string jobId, string cron) where TJob : class, IBackgroundJob
    {
        var options = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

        if (typeof(TJob).IsDefined(typeof(DisallowConcurrentExecutionAttribute), inherit: true))
        {
            _recurringJobManager.AddOrUpdate<RecurringJobRunner<TJob>>(
                jobId, runner => runner.RunWithoutConcurrency(CancellationToken.None), cron, options);
        }
        else
        {
            _recurringJobManager.AddOrUpdate<RecurringJobRunner<TJob>>(
                jobId, runner => runner.Run(CancellationToken.None), cron, options);
        }
    }
}