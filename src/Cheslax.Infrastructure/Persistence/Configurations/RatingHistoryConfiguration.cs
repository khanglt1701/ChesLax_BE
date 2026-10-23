using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cheslax.Infrastructure.Persistence.Configurations;

public sealed class RatingHistoryConfiguration : IEntityTypeConfiguration<RatingHistory>
{
    public void Configure(EntityTypeBuilder<RatingHistory> builder)
    {
        builder.ToTable("RatingHistories");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.UserId).HasMaxLength(450).IsRequired();
        builder.Property(entry => entry.OpponentUserId).HasMaxLength(450);
        builder.Property(entry => entry.OpponentName).HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.OldRankId).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.NewRankId).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.GameType).HasConversion<int>();
        builder.Property(entry => entry.Result).HasConversion<int>();
        builder.HasIndex(entry => new { entry.UserId, entry.CreatedAt });
        builder.HasIndex(entry => new { entry.MatchId, entry.UserId }).IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RatingHistories_Users_UserId");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.OpponentUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RatingHistories_Users_OpponentUserId");
        builder.HasOne<GameMatch>()
            .WithMany(match => match.RatingChanges)
            .HasForeignKey(entry => entry.MatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
