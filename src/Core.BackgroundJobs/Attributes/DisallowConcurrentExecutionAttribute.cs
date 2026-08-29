namespace Core.BackgroundJobs.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DisallowConcurrentExecutionAttribute : Attribute
{
    public DisallowConcurrentExecutionAttribute(int timeoutSeconds = 60)
    {
        if (timeoutSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "Timeout must not be negative");

        TimeoutSeconds = timeoutSeconds;
    }

    public int TimeoutSeconds { get; }
}
