using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.Progression
{
    /// <inheritdoc />
    public partial class AddCosmeticPieces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnlockedCosmeticPiece",
                schema: "progression",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PieceNumber = table.Column<short>(type: "smallint", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlockedCosmeticPiece", x => new { x.CharacterId, x.PieceNumber });
                });

            migrationBuilder.CreateTable(
                name: "WornCosmeticPiece",
                schema: "progression",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemSlot = table.Column<byte>(type: "smallint", nullable: false),
                    PieceNumber = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WornCosmeticPiece", x => new { x.CharacterId, x.ItemSlot });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnlockedCosmeticPiece",
                schema: "progression");

            migrationBuilder.DropTable(
                name: "WornCosmeticPiece",
                schema: "progression");
        }
    }
}
