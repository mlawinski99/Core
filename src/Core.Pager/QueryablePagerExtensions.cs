namespace Core.Pager;

public static class QueryablePagerExtensions
{
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, int page, int pageSize)
    {
        var normalizedPageSize = PagerSettings.Normalize(pageSize);
        return query
            .Skip((page - 1) * normalizedPageSize)
            .Take(normalizedPageSize);
    }

    public static IQueryable<T> TakePage<T>(this IQueryable<T> query, int pageSize)
    {
        return query.Take(PagerSettings.Normalize(pageSize));
    }
}
