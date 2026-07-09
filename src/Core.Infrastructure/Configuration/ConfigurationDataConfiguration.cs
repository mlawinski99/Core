using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Infrastructure.Configuration;

public class ConfigurationDataConfiguration : IEntityTypeConfiguration<ConfigurationData>
{
    public void Configure(EntityTypeBuilder<ConfigurationData> builder)
    {
        builder.ToTable("ConfigurationData");
        builder.HasKey(x => x.Key);
    }
}