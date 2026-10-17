using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.Progression
{
    /// <inheritdoc />
    public partial class AddCosmeticSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveCosmeticSet",
                schema: "progression",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetNumber = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveCosmeticSet", x => x.CharacterId);
                });

            migrationBuilder.CreateTable(
                name: "UnlockedCosmeticSet",
                schema: "progression",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetNumber = table.Column<short>(type: "smallint", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlockedCosmeticSet", x => new { x.CharacterId, x.SetNumber });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveCosmeticSet",
                schema: "progression");

            migrationBuilder.DropTable(
                name: "UnlockedCosmeticSet",
                schema: "progression");
        }
    }
}
