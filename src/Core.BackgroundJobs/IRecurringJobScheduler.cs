namespace Core.BackgroundJobs;

public interface IRecurringJobScheduler
{
    void ScheduleRecurring<TJob>(string jobId, string cron) where TJob : class, IBackgroundJob;
}
