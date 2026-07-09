using Core.Infrastructure;
using Core.DateTimeProvider;
using Core.DomainTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.DataAccessTypes;

public class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUserProvider _userProvider;

    public AuditableInterceptor(IDateTimeProvider dateTimeProvider, IUserProvider userProvider)
    {
        _dateTimeProvider = dateTimeProvider;
        _userProvider = userProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInfo(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                if (entry.Entity is IAuditableWithUser auditableWithUser)
                {
                    if (entry.State == EntityState.Added)
                    {
                        auditableWithUser.CreatedBy = _userProvider.UserId;
                        auditableWithUser.DateCreatedUtc = _dateTimeProvider.UtcNow;
                    }

                    auditableWithUser.ModifiedBy = _userProvider.UserId;
                    auditableWithUser.DateModifiedUtc = _dateTimeProvider.UtcNow;
                }
                else if (entry.Entity is IAuditable auditable)
                {
                    if (entry.State == EntityState.Added)
                        auditable.DateCreatedUtc = _dateTimeProvider.UtcNow;
                    auditable.DateModifiedUtc = _dateTimeProvider.UtcNow;
                }
            }
        }
    }
}