using Core.DomainTypes;
using Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.DataAccessTypes;

public class EncryptableInterceptor : SaveChangesInterceptor, IMaterializationInterceptor
{
    private readonly IEncryptor _encryptor;

    public EncryptableInterceptor(IEncryptor encryptor)
    {
        _encryptor = encryptor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EncryptEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EncryptEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EncryptEntities(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                EncryptEntity(entry.Entity);
            }
        }
    }

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        DecryptEntity(entity);
        return entity;
    }

    private void EncryptEntity(object entity)
    {
        var properties = entity.GetType().GetProperties()
            .Where(p => p.IsDefined(typeof(EncryptableAttribute), inherit: true)
                     && p.CanRead && p.CanWrite
                     && p.PropertyType == typeof(string));

        foreach (var prop in properties)
        {
            var plainText = (string)prop.GetValue(entity);
            if (!string.IsNullOrEmpty(plainText))
            {
                var encrypted = _encryptor.Encrypt(plainText);
                prop.SetValue(entity, encrypted);
            }
        }
    }

    private void DecryptEntity(object entity)
    {
        var properties = entity.GetType().GetProperties()
            .Where(p => p.IsDefined(typeof(EncryptableAttribute), inherit: true)
                     && p.CanRead && p.CanWrite
                     && p.PropertyType == typeof(string));

        foreach (var prop in properties)
        {
            var encrypted = (string)prop.GetValue(entity);
            if (!string.IsNullOrEmpty(encrypted))
            {
                var decrypted = _encryptor.Decrypt(encrypted);
                prop.SetValue(entity, decrypted);
            }
        }
    }
}
