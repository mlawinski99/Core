namespace Core.BackgroundJobs;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DisallowConcurrentExecutionAttribute : Attribute;
