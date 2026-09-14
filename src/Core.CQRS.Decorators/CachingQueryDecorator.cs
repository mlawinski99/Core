using Core.Caching;
using Core.ResultPattern;

namespace Core.CQRS.Decorators;

public sealed class CachingQueryDecorator<TQuery, TResult>(
    IRequestHandler<TQuery, TResult> requestHandler,
    ICacheService cacheService)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : IResult<TResult>
{
    private static readonly bool IsCacheable = typeof(ICacheable).IsAssignableFrom(typeof(TQuery));

    public async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        if (!IsCacheable)
            return await requestHandler.Handle(request, cancellationToken);

        var cacheable = (ICacheable)request;
        var key = $"{typeof(TQuery).FullName}:{cacheable.CacheKey}";

        var cached = await cacheService.Get<TResult>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var result = await requestHandler.Handle(request, cancellationToken);

        if (result.IsSuccess)
            await cacheService.Set(key, result, cacheable.CacheExpiration, cancellationToken);

        return result;
    }
}
