using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.Progression
{
    /// <inheritdoc />
    public partial class AddSeasonPass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeasonClaim",
                schema: "progression",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonClaim", x => new { x.AccountId, x.SeasonId, x.Level, x.IsPremium });
                });

            migrationBuilder.CreateTable(
                name: "SeasonPremium",
                schema: "progression",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPremium", x => new { x.AccountId, x.SeasonId });
                });

            migrationBuilder.CreateTable(
                name: "SeasonProgress",
                schema: "progression",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Experience = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonProgress", x => new { x.AccountId, x.SeasonId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeasonClaim",
                schema: "progression");

            migrationBuilder.DropTable(
                name: "SeasonPremium",
                schema: "progression");

            migrationBuilder.DropTable(
                name: "SeasonProgress",
                schema: "progression");
        }
    }
}
