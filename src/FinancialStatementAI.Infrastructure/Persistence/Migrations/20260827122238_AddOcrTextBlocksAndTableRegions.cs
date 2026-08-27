using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialStatementAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOcrTextBlocksAndTableRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConfidenceScore",
                table: "StatementExtractions",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OcrTableRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementExtractionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Html = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    X1 = table.Column<int>(type: "int", nullable: false),
                    Y1 = table.Column<int>(type: "int", nullable: false),
                    X2 = table.Column<int>(type: "int", nullable: false),
                    Y2 = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrTableRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrTableRegions_StatementExtractions_StatementExtractionId",
                        column: x => x.StatementExtractionId,
                        principalTable: "StatementExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcrTextBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementExtractionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    X1 = table.Column<int>(type: "int", nullable: false),
                    Y1 = table.Column<int>(type: "int", nullable: false),
                    X2 = table.Column<int>(type: "int", nullable: false),
                    Y2 = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrTextBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrTextBlocks_StatementExtractions_StatementExtractionId",
                        column: x => x.StatementExtractionId,
                        principalTable: "StatementExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcrTableRegions_StatementExtractionId",
                table: "OcrTableRegions",
                column: "StatementExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_OcrTextBlocks_StatementExtractionId",
                table: "OcrTextBlocks",
                column: "StatementExtractionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcrTableRegions");

            migrationBuilder.DropTable(
                name: "OcrTextBlocks");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "StatementExtractions");
        }
    }
}
