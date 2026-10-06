using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.Progression
{
    /// <inheritdoc />
    public partial class InitialProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "progression");

            migrationBuilder.CreateTable(
                name: "AchievementProgress",
                schema: "progression",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Count = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementProgress", x => new { x.OwnerId, x.AchievementId });
                });

            migrationBuilder.CreateTable(
                name: "ActiveTitle",
                schema: "progression",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveTitle", x => x.CharacterId);
                });

            migrationBuilder.CreateTable(
                name: "UnlockedTitle",
                schema: "progression",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlockedTitle", x => new { x.OwnerId, x.TitleId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AchievementProgress_AccountId",
                schema: "progression",
                table: "AchievementProgress",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AchievementProgress",
                schema: "progression");

            migrationBuilder.DropTable(
                name: "ActiveTitle",
                schema: "progression");

            migrationBuilder.DropTable(
                name: "UnlockedTitle",
                schema: "progression");
        }
    }
}
