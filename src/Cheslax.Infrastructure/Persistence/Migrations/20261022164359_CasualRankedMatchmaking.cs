using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cheslax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CasualRankedMatchmaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RatingHistories_MatchId",
                table: "RatingHistories");

            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "Rated",
                table: "GameRooms",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "GameRooms",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<bool>(
                name: "Rated",
                table: "GameMatches",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "GameMatches",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ActiveMatchLocks",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameType = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveMatchLocks", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ActiveMatchLocks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchmakingTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GameType = table.Column<int>(type: "int", nullable: false),
                    RatingAtEnqueue = table.Column<int>(type: "int", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConnectionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchmakingTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchmakingTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsGuest",
                table: "Users",
                column: "IsGuest");

            migrationBuilder.CreateIndex(
                name: "IX_RatingHistories_MatchId_UserId",
                table: "RatingHistories",
                columns: new[] { "MatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameRooms_CurrentMatchId",
                table: "GameRooms",
                column: "CurrentMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GameMatches_Status_Rated_Player1UserId",
                table: "GameMatches",
                columns: new[] { "Status", "Rated", "Player1UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameMatches_Status_Rated_Player2UserId",
                table: "GameMatches",
                columns: new[] { "Status", "Rated", "Player2UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveMatchLocks_MatchId",
                table: "ActiveMatchLocks",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchmakingTickets_GameType_Status_EnqueuedAt",
                table: "MatchmakingTickets",
                columns: new[] { "GameType", "Status", "EnqueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchmakingTickets_UserId_Status",
                table: "MatchmakingTickets",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveMatchLocks");

            migrationBuilder.DropTable(
                name: "MatchmakingTickets");

            migrationBuilder.DropIndex(
                name: "IX_Users_IsGuest",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_RatingHistories_MatchId_UserId",
                table: "RatingHistories");

            migrationBuilder.DropIndex(
                name: "IX_GameRooms_CurrentMatchId",
                table: "GameRooms");

            migrationBuilder.DropIndex(
                name: "IX_GameMatches_Status_Rated_Player1UserId",
                table: "GameMatches");

            migrationBuilder.DropIndex(
                name: "IX_GameMatches_Status_Rated_Player2UserId",
                table: "GameMatches");

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "GameRooms");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "GameMatches");

            migrationBuilder.AlterColumn<bool>(
                name: "Rated",
                table: "GameRooms",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "Rated",
                table: "GameMatches",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_RatingHistories_MatchId",
                table: "RatingHistories",
                column: "MatchId");
        }
    }
}
