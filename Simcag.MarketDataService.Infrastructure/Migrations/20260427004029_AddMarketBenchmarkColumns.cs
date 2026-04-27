using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simcag.MarketDataService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketBenchmarkColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketPriceHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CollectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPriceHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CollectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpenseCategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    GeographicRegion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SupplierProfile = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPrices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceHistory_CollectedDate",
                table: "MarketPriceHistory",
                column: "CollectedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceHistory_ProductName",
                table: "MarketPriceHistory",
                column: "ProductName");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_ExpenseCategory_GeographicRegion_IsActive",
                table: "MarketPrices",
                columns: new[] { "ExpenseCategory", "GeographicRegion", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_ProductName",
                table: "MarketPrices",
                column: "ProductName");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_ProductName_IsActive",
                table: "MarketPrices",
                columns: new[] { "ProductName", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketPriceHistory");

            migrationBuilder.DropTable(
                name: "MarketPrices");
        }
    }
}
