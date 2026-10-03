using Core.Caching;
using Core.CQRS;
using Core.CQRS.Decorators;
using Core.DataAccessTypes;
using Core.Logger;
using Core.ResultPattern;
using Core.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.UnitTests.CQRS.Decorators;

public class CqrsDecoratorsInstallerTests
{
    private static readonly string[] ExpectedCommandPipeline =
    [
        nameof(LoggingRequestDecorator<,>),
        nameof(ValidationRequestDecorator<,>),
        nameof(TransactionCommandDecorator<,>),
        nameof(RecordingCommandHandler),
        nameof(LoggingRequestDecorator<,>)
    ];

    private static readonly string[] ExpectedQueryPipeline =
    [
        nameof(LoggingRequestDecorator<,>),
        nameof(ValidationRequestDecorator<,>),
        nameof(CachingQueryDecorator<,>),
        nameof(RecordingQueryHandler),
        nameof(LoggingRequestDecorator<,>)
    ];

    [Fact]
    public async Task CommandHandler_WithAllDecorators_ShouldRunInPipelineOrder()
    {
        // Arrange
        using var scope = BuildProvider().CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RecordingCommand, Result>>();
        var calls = scope.ServiceProvider.GetRequiredService<List<string>>();

        // Act
        await handler.Handle(new RecordingCommand(), CancellationToken.None);

        // Assert
        calls.Should().Equal(ExpectedCommandPipeline);
    }

    [Fact]
    public async Task QueryHandler_WithAllDecorators_ShouldRunInPipelineOrder()
    {
        // Arrange
        using var scope = BuildProvider().CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<RecordingQuery, Result<int>>>();
        var calls = scope.ServiceProvider.GetRequiredService<List<string>>();

        // Act
        await handler.Handle(new RecordingQuery(), CancellationToken.None);

        // Assert
        calls.Should().Equal(ExpectedQueryPipeline);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddSingleton(typeof(IAppLogger<>), typeof(RecordingLogger<>));
        services.AddSingleton(typeof(IValidator<>), typeof(RecordingValidator<>));
        services.AddScoped<IUnitOfWork, RecordingUnitOfWork>();
        services.AddScoped<ICacheService, RecordingCacheService>();
        services.AddCqrs(typeof(TestCommandHandler).Assembly);
        services.AddCqrsDecorators();

        return services.BuildServiceProvider();
    }
}