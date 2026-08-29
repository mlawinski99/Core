using Core.Infrastructure.Extensions;
using Hangfire;
using Hangfire.Common;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Core.BackgroundJobs;

public static class BackgroundJobsDependencyInstaller
{
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services, IConfiguration configuration, string connectionStringName)
    {
        if (connectionStringName.IsNullOrEmpty())
            throw new ArgumentException($"{nameof(connectionStringName)} can not be null or empty string");

        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' was not found");

        services.AddOptions<BackgroundJobsOptions>()
            .Bind(configuration.GetSection(BackgroundJobsOptions.SectionName))
            .Validate(o => o.RetryAttempts >= 0, "BackgroundJobs:RetryAttempts must not be negative")
            .ValidateOnStart();

        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        var options = configuration.GetSection(BackgroundJobsOptions.SectionName).Get<BackgroundJobsOptions>()
                      ?? new BackgroundJobsOptions();

        JobFilterProviders.Providers.Add(new BackgroundJobFilterProvider(options.RetryAttempts));

        return services;
    }

    public static IServiceCollection AddRecurringJob<TJob>(
        this IServiceCollection services, string jobId, Func<IServiceProvider, string> cron)
        where TJob : class, IBackgroundJob
    {
        services.TryAddScoped<TJob>();

        var options = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

        // cron is read on start, so the schedule always matches the options the job sees
        services.AddSingleton<IHostedService>(sp => new RecurringJobScheduleHost(
            sp.GetRequiredService<IRecurringJobManager>(),
            manager => manager.AddOrUpdate<TJob>(jobId, job => job.Run(CancellationToken.None), cron(sp), options)));

        return services;
    }
}
