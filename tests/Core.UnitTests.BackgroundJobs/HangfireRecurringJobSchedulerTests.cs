using Core.BackgroundJobs;
using Hangfire;
using Hangfire.Common;
using NSubstitute;
using Xunit;

namespace Core.UnitTests.BackgroundJobs;

public class HangfireRecurringJobSchedulerTests
{
    private readonly IRecurringJobManager _recurringJobManager = Substitute.For<IRecurringJobManager>();
    private readonly HangfireRecurringJobScheduler _scheduler;

    public HangfireRecurringJobSchedulerTests()
    {
        _scheduler = new HangfireRecurringJobScheduler(_recurringJobManager);
    }

    private class TestJob : IBackgroundJob
    {
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [DisallowConcurrentExecution]
    private class TestNonConcurrentJob : IBackgroundJob
    {
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void ScheduleRecurring_JobWithoutAttribute_SchedulesRunMethod()
    {
        _scheduler.ScheduleRecurring<TestJob>("test-job", "*/30 * * * * *");

        _recurringJobManager.Received(1).AddOrUpdate(
            "test-job",
            Arg.Is<Job>(j => j.Type == typeof(RecurringJobRunner<TestJob>)
                             && j.Method.Name == nameof(RecurringJobRunner<TestJob>.Run)),
            "*/30 * * * * *",
            Arg.Any<RecurringJobOptions>());
    }

    [Fact]
    public void ScheduleRecurring_JobWithDisallowConcurrentExecution_SchedulesRunWithoutConcurrencyMethod()
    {
        _scheduler.ScheduleRecurring<TestNonConcurrentJob>("test-non-concurrent-job", "*/30 * * * * *");

        _recurringJobManager.Received(1).AddOrUpdate(
            "test-non-concurrent-job",
            Arg.Is<Job>(j => j.Type == typeof(RecurringJobRunner<TestNonConcurrentJob>)
                             && j.Method.Name == nameof(RecurringJobRunner<TestNonConcurrentJob>.RunWithoutConcurrency)),
            "*/30 * * * * *",
            Arg.Any<RecurringJobOptions>());
    }
}