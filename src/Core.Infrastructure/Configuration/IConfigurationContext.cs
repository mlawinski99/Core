using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure.Configuration;

public interface IConfigurationContext
{
    DbSet<ConfigurationData> ConfigurationData { get; set; }
}
