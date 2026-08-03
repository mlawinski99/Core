using Core.CQRS;
using Core.CQRS.Decorators;
using Core.DataAccessTypes;
using Core.Infrastructure.Json;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Core.IntegrationTests.Shared;

public abstract class IntegrationTestBase<TFixture> : IDisposable
    where TFixture : IntegrationTestFixtureBase
{
    private readonly IServiceScope _scope;

    protected TFixture Fixture { get; }
    protected TestDbContext Db { get; }
    protected ServiceProvider Services { get; }
    protected IRequestDispatcher Dispatcher { get; }

    protected IntegrationTestBase(TFixture fixture)
    {
        Fixture = fixture;

        var services = new ServiceCollection();
        ConfigureServices(services);

        Services = services.BuildServiceProvider();
        _scope = Services.CreateScope();

        Db = CreateDbContext();
        Dispatcher = _scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
    }

    protected virtual TestDbContext CreateDbContext() =>
        _scope.ServiceProvider.GetRequiredService<TestDbContext>();

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();
        services.AddAppLogger();
        services.AddSingleton<IJsonSerializer, TestJsonSerializer>();
        services.AddDbContext<TestDbContext>(options => options
            .UseNpgsql(Fixture.PostgresConnectionString)
            .EnableServiceProviderCaching(false));
        services.AddUnitOfWork<TestDbContext>();
        services.AddCqrs(GetType().Assembly);
        services.AddCqrsDecorators();
    }

    public virtual void Dispose()
    {
        Db.Dispose();
        _scope.Dispose();
        Services.Dispose();
    }
}