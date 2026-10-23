using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cheslax.Infrastructure.Persistence.Configurations;

public sealed class GameMatchConfiguration : IEntityTypeConfiguration<GameMatch>
{
    public void Configure(EntityTypeBuilder<GameMatch> builder)
    {
        builder.ToTable("GameMatches");
        builder.HasKey(match => match.Id);
        builder.Property(match => match.TimeControlId).HasMaxLength(16).IsRequired();
        builder.Property(match => match.Player1UserId).HasMaxLength(450).IsRequired();
        builder.Property(match => match.Player2UserId).HasMaxLength(450).IsRequired();
        builder.Property(match => match.Player1Side).HasMaxLength(8).IsRequired();
        builder.Property(match => match.Player2Side).HasMaxLength(8).IsRequired();
        builder.Property(match => match.GameType).HasConversion<int>();
        builder.Property(match => match.Mode).HasConversion<int>().HasDefaultValue(MatchMode.Casual).HasSentinel((MatchMode)0);
        builder.Property(match => match.Status).HasConversion<int>();
        builder.Property(match => match.Rated).HasDefaultValue(false);
        builder.Property(match => match.OpponentType).HasConversion<int>().HasDefaultValue(OpponentType.Human).HasSentinel((OpponentType)0);
        builder.Property(match => match.AiDifficulty).HasMaxLength(32);
        builder.Property(match => match.AiDifficultyLabel).HasMaxLength(40);
        builder.Property(match => match.AiRatingPolicy).HasMaxLength(64);
        builder.HasIndex(match => new { match.Status, match.Rated, match.Player1UserId });
        builder.HasIndex(match => new { match.Status, match.Rated, match.Player2UserId });
        builder.HasIndex(match => new { match.OpponentType, match.Mode, match.Status });
        builder.Property(match => match.Outcome).HasConversion<int>();
        builder.Property(match => match.ResultReason).HasConversion<int>();
        builder.Property(match => match.RowVersion).IsRowVersion();
        builder.HasIndex(match => match.Player1UserId);
        builder.HasIndex(match => match.Player2UserId);
        builder.HasIndex(match => new { match.GameType, match.FinishedAt });
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(match => match.Player1UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_GameMatches_Users_Player1UserId");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(match => match.Player2UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_GameMatches_Users_Player2UserId");
        builder.HasMany(match => match.Moves)
            .WithOne(move => move.Match)
            .HasForeignKey(move => move.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MatchMoveConfiguration : IEntityTypeConfiguration<MatchMove>
{
    public void Configure(EntityTypeBuilder<MatchMove> builder)
    {
        builder.ToTable("MatchMoves");
        builder.HasKey(move => move.Id);
        builder.Property(move => move.PlayerUserId).HasMaxLength(450).IsRequired();
        builder.Property(move => move.Payload).HasMaxLength(512).IsRequired();
        builder.HasIndex(move => new { move.MatchId, move.Sequence }).IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(move => move.PlayerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
