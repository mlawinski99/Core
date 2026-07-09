using System.Diagnostics;
using System.Linq.Expressions;
using Core.Outbox;
using Core.DomainTypes;
using Core.Infrastructure.Configuration;
using Core.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.DataAccessTypes;

public abstract class BaseDbContext : DbContext, IUnitOfWork, IConfigurationContext
{
    public DbSet<ConfigurationData> ConfigurationData { get; set; }

    private readonly IJsonSerializer _jsonSerializer;
    private readonly IEnumerable<IInterceptor> _interceptors;

    protected BaseDbContext(DbContextOptions options,
        IJsonSerializer jsonSerializer,
        IEnumerable<IInterceptor> interceptors)
        : base(options)
    {
        _jsonSerializer = jsonSerializer;
        _interceptors = interceptors;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_interceptors.Any())
        {
            optionsBuilder.AddInterceptors(_interceptors);
            optionsBuilder.EnableServiceProviderCaching(false);
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ConfigurationDataConfiguration());

        ApplySoftDeleteQueryFilters(modelBuilder);
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(property), parameter);

                entityType.SetQueryFilter(filter);
            }
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (this is IOutbox)
        {
            await AddOutboxMessagesAsync(this as IOutbox, cancellationToken);
        }
        
        return await base.SaveChangesAsync(cancellationToken);
    }
    private async Task AddOutboxMessagesAsync(IOutbox outboxContext, CancellationToken cancellationToken)
    {
        var domainEvents = ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        var correlationId = Activity.Current?.TraceId.ToString();

        foreach (var domainEvent in domainEvents)
        {
            var message = new OutboxMessage
            {
                OccurredOnUtc = domainEvent.OccurredOnUtc,
                Type = domainEvent.GetType().FullName!,
                Content = _jsonSerializer.Serialize(domainEvent),
                CorrelationId = correlationId,
                IsProcessed = false,
                ProcessedOn = null
            };

            outboxContext.OutboxMessages.Add(message);
        }

        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
            entry.Entity.ClearDomainEvents();

        await Task.CompletedTask;
    }
}