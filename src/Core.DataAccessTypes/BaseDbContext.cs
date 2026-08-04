using System.Diagnostics;
using System.Linq.Expressions;
using Core.DomainTypes;
using Core.Infrastructure.Configuration;
using Core.Infrastructure.Json;
using Core.Outbox;
using Core.ResultPattern;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.DataAccessTypes;

public abstract class BaseDbContext(
    DbContextOptions options,
    IJsonSerializer jsonSerializer,
    IEnumerable<IInterceptor> interceptors)
    : DbContext(options), IUnitOfWork, IConfigurationContext
{
    public DbSet<ConfigurationData> ConfigurationData { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (interceptors.Any())
        {
            optionsBuilder.AddInterceptors(interceptors);
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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var messages = AddOutboxMessages();

        int result;
        try
        {
            result = base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch
        {
            DetachOutboxMessages(messages);
            throw;
        }

        ClearDomainEvents();

        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var messages = AddOutboxMessages();

        int result;
        try
        {
            result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            DetachOutboxMessages(messages);
            throw;
        }

        ClearDomainEvents();

        return result;
    }

    private IResultState? _firstFailure;

    public void EnsureNoActiveTransaction(string commandName)
    {
        if (Database.CurrentTransaction is null)
            return;

        _firstFailure ??= Result.InternalError();

        throw new InvalidOperationException(
            $"{commandName} is non-transactional and cannot run inside an active transaction.");
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) where T : IResult<T>
    {
        //multiple commands in 1 scope, needed
        if (Database.CurrentTransaction is not null)
        {
            T nested;
            try
            {
                nested = await operation(cancellationToken);
            }
            catch
            {
                _firstFailure ??= Result.InternalError();
                throw;
            }

            if (nested is not { IsSuccess: true })
                _firstFailure ??= nested;

            return nested;
        }

        _firstFailure = null;

        // @TODO retry strategy
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        T result;
        try
        {
            result = await operation(cancellationToken);
        }
        catch
        {
            // @TODO retry strategy - reset state between attempts
            ChangeTracker.Clear();
            throw;
        }

        try
        {
            if (result is { IsSuccess: true } && _firstFailure is null)
            {
                // @TODO retry strategy
                await transaction.CommitAsync(cancellationToken);

                return result;
            }

            await transaction.RollbackAsync(cancellationToken);
            ChangeTracker.Clear();
        }
        catch
        {
            ChangeTracker.Clear();
            throw;
        }

        // 1st command can return success, but one in chain failed
        return result is { IsSuccess: true }
            ? T.Failure(_firstFailure!.Code, _firstFailure.Error)
            : result;
    }

    private List<OutboxMessage> AddOutboxMessages()
    {
        if (this is not IOutbox outboxContext)
            return [];

        var domainEvents = ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        var correlationId = Activity.Current?.TraceId.ToString();

        var messages = new List<OutboxMessage>();

        foreach (var domainEvent in domainEvents)
        {
            var message = new OutboxMessage
            {
                OccurredOnUtc = domainEvent.OccurredOnUtc,
                Type = domainEvent.GetType().FullName!,
                Content = jsonSerializer.Serialize(domainEvent),
                CorrelationId = correlationId,
                IsProcessed = false,
                ProcessedOn = null
            };

            outboxContext.OutboxMessages.Add(message);
            messages.Add(message);
        }

        return messages;
    }

    private void DetachOutboxMessages(List<OutboxMessage> messages)
    {
        foreach (var message in messages)
            Entry(message).State = EntityState.Detached;
    }

    private void ClearDomainEvents()
    {
        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
            entry.Entity.ClearDomainEvents();
    }
}
