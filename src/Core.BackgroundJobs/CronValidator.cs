using Cronos;

namespace Core.BackgroundJobs;

public static class CronValidator
{
    public static bool IsValid(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
            return false;

        var format = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6
            ? CronFormat.IncludeSeconds
            : CronFormat.Standard;

        return CronExpression.TryParse(cron, format, out _);
    }
}
