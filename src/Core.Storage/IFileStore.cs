using Microsoft.EntityFrameworkCore;

namespace Core.Storage;

public interface IFileStore
{
    DbSet<StoredFile> StoredFiles { get; set; }
}