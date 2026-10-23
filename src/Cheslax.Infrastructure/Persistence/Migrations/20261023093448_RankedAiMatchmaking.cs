using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cheslax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RankedAiMatchmaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBot",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AiDifficulty",
                table: "GameMatches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiDifficultyLabel",
                table: "GameMatches",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiRatingPolicy",
                table: "GameMatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpponentType",
                table: "GameMatches",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsBot",
                table: "Users",
                column: "IsBot");

            migrationBuilder.CreateIndex(
                name: "IX_GameMatches_OpponentType_Mode_Status",
                table: "GameMatches",
                columns: new[] { "OpponentType", "Mode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsBot",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_GameMatches_OpponentType_Mode_Status",
                table: "GameMatches");

            migrationBuilder.DropColumn(
                name: "IsBot",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AiDifficulty",
                table: "GameMatches");

            migrationBuilder.DropColumn(
                name: "AiDifficultyLabel",
                table: "GameMatches");

            migrationBuilder.DropColumn(
                name: "AiRatingPolicy",
                table: "GameMatches");

            migrationBuilder.DropColumn(
                name: "OpponentType",
                table: "GameMatches");
        }
    }
}
