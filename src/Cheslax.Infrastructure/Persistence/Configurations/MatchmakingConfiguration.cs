using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cheslax.Infrastructure.Persistence.Configurations;

public sealed class MatchmakingTicketConfiguration : IEntityTypeConfiguration<MatchmakingTicket>
{
    public void Configure(EntityTypeBuilder<MatchmakingTicket> builder)
    {
        builder.ToTable("MatchmakingTickets");
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.UserId).HasMaxLength(450).IsRequired();
        builder.Property(ticket => ticket.ConnectionId).HasMaxLength(128);
        builder.Property(ticket => ticket.GameType).HasConversion<int>();
        builder.Property(ticket => ticket.Status).HasConversion<int>();
        builder.HasIndex(ticket => new { ticket.UserId, ticket.Status });
        builder.HasIndex(ticket => new { ticket.GameType, ticket.Status, ticket.EnqueuedAt });
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ticket => ticket.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ActiveMatchLockConfiguration : IEntityTypeConfiguration<ActiveMatchLock>
{
    public void Configure(EntityTypeBuilder<ActiveMatchLock> builder)
    {
        builder.ToTable("ActiveMatchLocks");
        builder.HasKey(item => item.UserId);
        builder.Property(item => item.UserId).HasMaxLength(450);
        builder.Property(item => item.GameType).HasConversion<int>();
        builder.Property(item => item.Mode).HasConversion<int>();
        builder.HasIndex(item => item.MatchId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
