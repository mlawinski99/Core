namespace Core.BackgroundJobs;

public class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    public int RetryAttempts { get; init; } = 3;
}
