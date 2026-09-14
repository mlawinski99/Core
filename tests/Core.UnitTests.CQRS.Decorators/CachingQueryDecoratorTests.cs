using Core.Caching;
using Core.CQRS;
using Core.CQRS.Decorators;
using Core.ResultPattern;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Core.UnitTests.CQRS.Decorators;

public class CachingQueryDecoratorTests
{
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();

    [Fact]
    public async Task Handle_WhenEntryIsCached_ShouldReturnItWithoutCallingHandler()
    {
        var handler = Substitute.For<IRequestHandler<CacheableTestQuery, Result<int>>>();
        _cacheService.Get<Result<int>>("Core.UnitTests.CQRS.Decorators.CacheableTestQuery:7", Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(99));
        var decorator = new CachingQueryDecorator<CacheableTestQuery, Result<int>>(handler, _cacheService);

        var result = await decorator.Handle(new CacheableTestQuery(7), CancellationToken.None);

        result.Data.Should().Be(99);
        await handler.DidNotReceive().Handle(Arg.Any<CacheableTestQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEntryIsMissing_ShouldCallHandlerAndCacheSuccess()
    {
        var handler = Substitute.For<IRequestHandler<CacheableTestQuery, Result<int>>>();
        var handlerResult = Result<int>.Success(14);
        handler.Handle(Arg.Any<CacheableTestQuery>(), Arg.Any<CancellationToken>()).Returns(handlerResult);
        var decorator = new CachingQueryDecorator<CacheableTestQuery, Result<int>>(handler, _cacheService);

        var result = await decorator.Handle(new CacheableTestQuery(7), CancellationToken.None);

        result.Should().BeSameAs(handlerResult);
        await _cacheService.Received(1)
            .Set("Core.UnitTests.CQRS.Decorators.CacheableTestQuery:7", handlerResult, TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenResultIsFailure_ShouldNotCacheIt()
    {
        var handler = Substitute.For<IRequestHandler<CacheableTestQuery, Result<int>>>();
        handler.Handle(Arg.Any<CacheableTestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.NotFound("missing"));
        var decorator = new CachingQueryDecorator<CacheableTestQuery, Result<int>>(handler, _cacheService);

        var result = await decorator.Handle(new CacheableTestQuery(7), CancellationToken.None);

        result.Code.Should().Be(ResultCode.NotFound);
        await _cacheService.DidNotReceiveWithAnyArgs()
            .Set(Arg.Any<string>(), Arg.Any<Result<int>>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenQueryIsNotCacheable_ShouldNotCache()
    {
        var handler = Substitute.For<IRequestHandler<TestQuery, Result<int>>>();
        handler.Handle(Arg.Any<TestQuery>(), Arg.Any<CancellationToken>()).Returns(Result<int>.Success(4));
        var decorator = new CachingQueryDecorator<TestQuery, Result<int>>(handler, _cacheService);

        var result = await decorator.Handle(new TestQuery(2), CancellationToken.None);

        result.Data.Should().Be(4);
        await handler.Received(1).Handle(Arg.Any<TestQuery>(), Arg.Any<CancellationToken>());
        await _cacheService.DidNotReceiveWithAnyArgs().Get<Result<int>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cacheService.DidNotReceiveWithAnyArgs()
            .Set(Arg.Any<string>(), Arg.Any<Result<int>>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }
}
