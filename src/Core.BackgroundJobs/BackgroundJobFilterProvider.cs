using System.Reflection;
using Core.BackgroundJobs.Attributes;
using Hangfire;
using Hangfire.Common;

namespace Core.BackgroundJobs;

// translates Core attributes into Hangfire filters, so jobs never reference Hangfire
public class BackgroundJobFilterProvider(int defaultRetryAttempts) : IJobFilterProvider
{
    public IEnumerable<JobFilter> GetFilters(Job job)
    {
        var concurrency = job.Type.GetCustomAttribute<DisallowConcurrentExecutionAttribute>(inherit: true);

        if (concurrency is not null)
        {
            yield return new JobFilter(
                new DisableConcurrentExecutionAttribute(concurrency.TimeoutSeconds), JobFilterScope.Type, order: null);
        }

        var attempts = job.Type.GetCustomAttribute<RetryAttribute>(inherit: true)?.Attempts ?? defaultRetryAttempts;

        yield return new JobFilter(
            new AutomaticRetryAttribute { Attempts = attempts, OnAttemptsExceeded = AttemptsExceededAction.Delete },
            JobFilterScope.Type,
            order: null);
    }
}
