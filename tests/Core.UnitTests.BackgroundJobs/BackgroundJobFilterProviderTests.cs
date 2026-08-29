using Core.BackgroundJobs;
using Core.BackgroundJobs.Attributes;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Xunit;

namespace Core.UnitTests.BackgroundJobs;

public class BackgroundJobFilterProviderTests
{
    private readonly BackgroundJobFilterProvider _provider = new(defaultRetryAttempts: 3);

    private class TestJob : IBackgroundJob
    {
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [DisallowConcurrentExecution(900)]
    private class TestNonConcurrentJob : IBackgroundJob
    {
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Retry(0)]
    private class TestNoRetryJob : IBackgroundJob
    {
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [DisallowConcurrentExecution(900)]
    [Retry(0)]
    private class TestNonConcurrentNoRetryJob : IBackgroundJob
    {
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void GetFilters_JobWithBothAttributes_AddsBothFilters()
    {
        var filters = _provider
            .GetFilters(Job.FromExpression<TestNonConcurrentNoRetryJob>(j => j.Run(CancellationToken.None)))
            .Select(f => f.Instance)
            .ToList();

        filters.Should().ContainSingle(f => f is DisableConcurrentExecutionAttribute);
        filters.OfType<AutomaticRetryAttribute>().Single().Attempts.Should().Be(0);
    }

    [Fact]
    public void GetFilters_JobWithDisallowConcurrentExecution_AddsDisableConcurrentExecutionFilter()
    {
        var filters = _provider
            .GetFilters(Job.FromExpression<TestNonConcurrentJob>(j => j.Run(CancellationToken.None)))
            .Select(f => f.Instance)
            .ToList();

        filters.Should().ContainSingle(f => f is DisableConcurrentExecutionAttribute);
    }

    [Fact]
    public void GetFilters_JobWithoutDisallowConcurrentExecution_AddsNoDisableConcurrentExecutionFilter()
    {
        var filters = _provider
            .GetFilters(Job.FromExpression<TestJob>(j => j.Run(CancellationToken.None)))
            .Select(f => f.Instance)
            .ToList();

        filters.Should().NotContain(f => f is DisableConcurrentExecutionAttribute);
    }

    [Fact]
    public void GetFilters_JobWithoutRetryAttribute_RetriesTheConfiguredDefault()
    {
        var retry = _provider
            .GetFilters(Job.FromExpression<TestJob>(j => j.Run(CancellationToken.None)))
            .Select(f => f.Instance)
            .OfType<AutomaticRetryAttribute>()
            .Single();

        retry.Attempts.Should().Be(3);
    }

    [Fact]
    public void GetFilters_JobWithRetryAttribute_OverridesTheConfiguredDefault()
    {
        var retry = _provider
            .GetFilters(Job.FromExpression<TestNoRetryJob>(j => j.Run(CancellationToken.None)))
            .Select(f => f.Instance)
            .OfType<AutomaticRetryAttribute>()
            .Single();

        retry.Attempts.Should().Be(0);
    }
}
