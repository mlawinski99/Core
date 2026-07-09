using Core.DomainTypes;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.DataAccessTypes;

public static class EntityTypeBuilderExtensions
{
    public static EntityTypeBuilder<TEntity> WithId<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        builder.HasKey(e => e.Id);

        return builder;
    }
    
    public static EntityTypeBuilder<TEntity> WithAuditable<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity, IAuditable
    {
        builder.Property(e => e.DateCreatedUtc);
        builder.Property(e => e.DateModifiedUtc);

        return builder;
    }

    public static EntityTypeBuilder<TEntity> WithAuditableWithUser<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity, IAuditableWithUser
    {
        builder.Property(e => e.DateCreatedUtc);
        builder.Property(e => e.DateModifiedUtc);
        builder.Property(e => e.CreatedBy);
        builder.Property(e => e.ModifiedBy);

        return builder;
    }

    public static EntityTypeBuilder<TEntity> WithSoftDeletable<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity, ISoftDeletable
    {
        builder.Property(e => e.DateDeletedUtc);
        builder.Property(e => e.IsDeleted);

        return builder;
    }
    
    public static EntityTypeBuilder<TEntity> WithVersionable<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity, IVersionable
    {
        builder.Property(e => e.VersionId);
        builder.Property(e => e.VersionGroupId);

        return builder;
    }
}