using Cheslax.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cheslax.Infrastructure.Persistence;

public class CheslaxDbContext(DbContextOptions<CheslaxDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CheslaxDbContext).Assembly);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(40).IsRequired();
            entity.Property(user => user.AvatarId).HasMaxLength(32).IsRequired();
        });
    }
}
