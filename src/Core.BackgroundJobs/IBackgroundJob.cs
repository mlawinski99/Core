namespace Core.BackgroundJobs;

public interface IBackgroundJob
{
    Task Run(CancellationToken cancellationToken);
}