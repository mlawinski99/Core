using Core.DomainTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Core.DataAccessTypes;

public class VersionableInterceptor : SaveChangesInterceptor
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