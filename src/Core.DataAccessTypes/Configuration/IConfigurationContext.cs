using Microsoft.EntityFrameworkCore;

namespace Core.DataAccessTypes.Configuration;

public interface IConfigurationContext
{
    DbSet<ConfigurationData> ConfigurationData { get; set; }
}