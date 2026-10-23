using Cheslax.Domain;
using Cheslax.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cheslax.Infrastructure.Persistence.Configurations;

public sealed class GameRoomConfiguration : IEntityTypeConfiguration<GameRoom>
{
    public void Configure(EntityTypeBuilder<GameRoom> builder)
    {
        builder.ToTable("GameRooms");
        builder.HasKey(room => room.Id);
        builder.Property(room => room.Code).HasMaxLength(8).IsRequired();
        builder.Property(room => room.TimeControlId).HasMaxLength(16).IsRequired();
        builder.Property(room => room.HostUserId).HasMaxLength(450).IsRequired();
        builder.Property(room => room.DrawOfferedByUserId).HasMaxLength(450);
        builder.Property(room => room.ClockRunningSide).HasMaxLength(8);
        builder.Property(room => room.GameType).HasConversion<int>();
        builder.Property(room => room.Mode).HasConversion<int>().HasDefaultValue(MatchMode.Casual).HasSentinel((MatchMode)0);
        builder.Property(room => room.Status).HasConversion<int>();
        builder.Property(room => room.Rated).HasDefaultValue(false);
        builder.Property(room => room.StateJson);
        builder.Property(room => room.RowVersion).IsRowVersion();
        builder.HasIndex(room => room.Code).IsUnique();
        builder.HasIndex(room => new { room.Status, room.LastActivityAt });
        builder.HasIndex(room => room.CurrentMatchId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(room => room.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(room => room.Seats)
            .WithOne(seat => seat.Room)
            .HasForeignKey(seat => seat.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(room => room.Matches)
            .WithOne(match => match.Room)
            .HasForeignKey(match => match.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GameRoomSeatConfiguration : IEntityTypeConfiguration<GameRoomSeat>
{
    public void Configure(EntityTypeBuilder<GameRoomSeat> builder)
    {
        builder.ToTable("GameRoomSeats");
        builder.HasKey(seat => seat.Id);
        builder.Property(seat => seat.UserId).HasMaxLength(450).IsRequired();
        builder.Property(seat => seat.Side).HasMaxLength(8).IsRequired();
        builder.HasIndex(seat => new { seat.RoomId, seat.UserId }).IsUnique();
        builder.HasIndex(seat => new { seat.RoomId, seat.SeatNumber }).IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(seat => seat.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
