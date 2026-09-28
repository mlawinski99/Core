using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Storage;

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("StoredFiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired();
        builder.Property(x => x.FileName).IsRequired();
        builder.Property(x => x.ContentType).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => new { x.Status, x.DateCreatedUtc });
    }
}