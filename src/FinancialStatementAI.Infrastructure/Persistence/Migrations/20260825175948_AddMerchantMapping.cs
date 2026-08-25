using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialStatementAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantPattern = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MatchType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsSystemDefined = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantMappings_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantMappings_CategoryId",
                table: "MerchantMappings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantMappings_MerchantPattern",
                table: "MerchantMappings",
                column: "MerchantPattern");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantMappings");
        }
    }
}
