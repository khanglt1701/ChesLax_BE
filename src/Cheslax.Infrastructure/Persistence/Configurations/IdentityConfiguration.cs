using Cheslax.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cheslax.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(user => user.DisplayName).HasMaxLength(40).IsRequired();
        builder.Property(user => user.AvatarId).HasMaxLength(32).IsRequired();
        builder.Property(user => user.IsGuest).HasDefaultValue(false);
        builder.Property(user => user.IsBot).HasDefaultValue(false);
        builder.HasIndex(user => user.IsGuest);
        builder.HasIndex(user => user.IsBot);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder) => builder.ToTable("Roles");
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder) => builder.ToTable("UserRoles");
}

public sealed class UserClaimConfiguration : IEntityTypeConfiguration<UserClaim>
{
    public void Configure(EntityTypeBuilder<UserClaim> builder) => builder.ToTable("UserClaims");
}

public sealed class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder) => builder.ToTable("UserLogins");
}

public sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder) => builder.ToTable("UserTokens");
}

public sealed class RoleClaimConfiguration : IEntityTypeConfiguration<RoleClaim>
{
    public void Configure(EntityTypeBuilder<RoleClaim> builder) => builder.ToTable("RoleClaims");
}
