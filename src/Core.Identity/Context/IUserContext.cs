using Core.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Identity.Context;

public interface IUserContext
{
    DbSet<User> Users { get; set; }
}