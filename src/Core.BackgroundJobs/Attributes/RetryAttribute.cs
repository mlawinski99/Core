namespace Core.BackgroundJobs.Attributes;

// overrides BackgroundJobs:RetryAttempts for a single job
[AttributeUsage(AttributeTargets.Class)]
public sealed class RetryAttribute : Attribute
{
    public RetryAttribute(int attempts)
    {
        if (attempts < 0)
            throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Attempts must not be negative");

        Attempts = attempts;
    }

    public int Attempts { get; }
}
