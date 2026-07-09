namespace Core.Pager;

public static class PagerSettings
{
    public const int MaxPageSize = 20;

    public static int Normalize(int pageSize) => Math.Min(pageSize, MaxPageSize);
}