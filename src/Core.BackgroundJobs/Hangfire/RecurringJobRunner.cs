using Hangfire;

namespace Core.BackgroundJobs;

public class RecurringJobRunner<TJob> where TJob : class, IBackgroundJob
{
    private readonly TJob _job;

    public RecurringJobRunner(TJob job)
    {
        _job = job;
    }

    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task Run(CancellationToken cancellationToken) => _job.Run(cancellationToken);

    [DisableConcurrentExecution(60)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task RunWithoutConcurrency(CancellationToken cancellationToken) => _job.Run(cancellationToken);
}
