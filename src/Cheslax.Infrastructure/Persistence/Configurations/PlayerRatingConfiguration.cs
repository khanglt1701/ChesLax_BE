using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cheslax.Infrastructure.Persistence.Configurations;

public sealed class PlayerRatingConfiguration : IEntityTypeConfiguration<PlayerRating>
{
    public void Configure(EntityTypeBuilder<PlayerRating> builder)
    {
        builder.ToTable("PlayerRatings");
        builder.HasKey(rating => rating.Id);
        builder.Property(rating => rating.UserId).HasMaxLength(450).IsRequired();
        builder.Property(rating => rating.GameType).HasConversion<int>();
        builder.HasIndex(rating => new { rating.UserId, rating.GameType }).IsUnique();
        builder.HasIndex(rating => new { rating.GameType, rating.Rating });
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(rating => rating.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
