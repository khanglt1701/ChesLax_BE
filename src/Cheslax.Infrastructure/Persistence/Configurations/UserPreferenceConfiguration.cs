using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cheslax.Infrastructure.Persistence.Configurations;

public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("UserPreferences");
        builder.HasKey(preference => preference.Id);
        builder.Property(preference => preference.UserId).HasMaxLength(450).IsRequired();
        builder.Property(preference => preference.Theme).HasMaxLength(16).IsRequired();
        builder.Property(preference => preference.ChessBoardTheme).HasMaxLength(32).IsRequired();
        builder.Property(preference => preference.ChessPieceSet).HasMaxLength(32).IsRequired();
        builder.Property(preference => preference.OAnQuanTheme).HasMaxLength(32).IsRequired();
        builder.HasIndex(preference => preference.UserId).IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
