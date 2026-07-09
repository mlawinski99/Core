namespace Core.Pager;

public class CursorPagedResult<T>
{
    public List<T> Items { get; }
    public bool HasMore { get; }

    public CursorPagedResult(List<T> items, bool hasMore)
    {
        Items = items;
        HasMore = hasMore;
    }
}
