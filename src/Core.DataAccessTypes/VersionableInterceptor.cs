using Core.DomainTypes;
using Core.Infrastructure;
using Core.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Core.DataAccessTypes;

public class VersionableInterceptor(
    IExpectedVersionProvider expectedVersionProvider,
    IAppLogger<VersionableInterceptor> logger) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyVersioning(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyVersioning(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyVersioning(DbContext? context)
    {
        if (context == null) return;

        var entries = context.ChangeTracker
            .Entries()
            .Where(e =>
                e.State == EntityState.Modified &&
                e.Entity is IVersionable)
            .ToList();

        EnsureExpectedVersionIsCurrent(entries);

        foreach (var entry in entries)
        {
            var originalId = (entry.Entity as Entity)?.Id;
            var historicalClone = CloneFromOriginalValues(entry);

            if (historicalClone is Entity clonedEntity)
                clonedEntity.Id = Guid.NewGuid();

            if (historicalClone is IVersionable clonedVersionable)
                clonedVersionable.VersionGroupId = clonedVersionable.VersionGroupId ?? originalId;

            if (entry.Entity is IVersionable original)
            {
                original.VersionId += 1;
                entry.Property(nameof(IVersionable.VersionGroupId)).CurrentValue ??= originalId;
            }

            context.Add(historicalClone);
        }
    }

    // the version the client edited must still be the newest one stored
    private void EnsureExpectedVersionIsCurrent(List<EntityEntry> entries)
    {
        var expectedVersion = expectedVersionProvider.ExpectedVersion;

        if (expectedVersion is null || entries.Count == 0)
            return;

        if (entries.Count > 1)
        {
            logger.LogWarning(
                "Expected version ignored, {Count} versionable entities were modified in one save", entries.Count);
            return;
        }

        var currentVersion = (int)entries[0].Property(nameof(IVersionable.VersionId)).OriginalValue;

        if (expectedVersion != currentVersion)
        {
            throw new DbUpdateConcurrencyException(
                $"Expected version {expectedVersion} but current version is {currentVersion}");
        }
    }

    private static object CloneFromOriginalValues(EntityEntry entry)
    {
        var clone = entry.OriginalValues.ToObject();

        foreach (var reference in entry.References)
        {
            if (reference.Metadata is not INavigation nav || !nav.ForeignKey.IsOwnership || reference.TargetEntry is null)
                continue;

            var ownedClone = reference.TargetEntry.OriginalValues.ToObject();
            var prop = clone.GetType().GetProperty(reference.Metadata.Name);
            prop?.SetValue(clone, ownedClone);
        }

        return clone;
    }
}
