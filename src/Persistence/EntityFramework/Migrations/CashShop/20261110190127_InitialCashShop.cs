using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.CashShop
{
    /// <inheritdoc />
    public partial class InitialCashShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cashshop");

            migrationBuilder.CreateTable(
                name: "Balance",
                schema: "cashshop",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balance", x => x.AccountId);
                    table.CheckConstraint("CK_Balance_Points", "\"Points\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "StorageItem",
                schema: "cashshop",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageSeq = table.Column<int>(type: "integer", nullable: false),
                    PriceSeq = table.Column<int>(type: "integer", nullable: false),
                    ProductSeq = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemLevel = table.Column<byte>(type: "smallint", nullable: false),
                    Quantity = table.Column<byte>(type: "smallint", nullable: false),
                    Skill = table.Column<bool>(type: "boolean", nullable: false),
                    Luck = table.Column<bool>(type: "boolean", nullable: false),
                    OptionLevel = table.Column<byte>(type: "smallint", nullable: false),
                    ExcellentOptions = table.Column<byte>(type: "smallint", nullable: false),
                    IsSkin = table.Column<bool>(type: "boolean", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transaction",
                schema: "cashshop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    PointsAfter = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageItem_AccountId_ConsumedAt",
                schema: "cashshop",
                table: "StorageItem",
                columns: new[] { "AccountId", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_AccountId_CreatedAt",
                schema: "cashshop",
                table: "Transaction",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_AccountId_Reference",
                schema: "cashshop",
                table: "Transaction",
                columns: new[] { "AccountId", "Reference" },
                unique: true,
                filter: "\"Reference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Balance",
                schema: "cashshop");

            migrationBuilder.DropTable(
                name: "StorageItem",
                schema: "cashshop");

            migrationBuilder.DropTable(
                name: "Transaction",
                schema: "cashshop");
        }
    }
}
