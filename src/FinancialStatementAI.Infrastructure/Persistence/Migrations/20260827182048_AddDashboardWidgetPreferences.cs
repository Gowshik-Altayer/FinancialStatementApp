using System;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialStatementAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardWidgetPreferences : Migration
    {
        private static readonly string[] SeedColumns = ["Id", "Role", "UserId", "WidgetKey", "SortOrder", "IsVisible", "UpdatedAt"];

        // Fixed timestamp (not DateTime.UtcNow) so re-running this migration's generated SQL is
        // byte-identical across environments — matches how every other seeded/generated value in
        // this migration must be deterministic for EF's model snapshot comparison to stay stable.
        private static readonly DateTime SeedTimestamp = new(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc);

        private static object[,] BuildGrid(System.Collections.Generic.IReadOnlyList<(string WidgetKey, bool Visible)> defaults, string role)
        {
            var grid = new object[defaults.Count, 7];
            for (var i = 0; i < defaults.Count; i++)
            {
                grid[i, 0] = Guid.NewGuid();
                grid[i, 1] = role;
                grid[i, 2] = null;
                grid[i, 3] = defaults[i].WidgetKey;
                grid[i, 4] = i;
                grid[i, 5] = defaults[i].Visible;
                grid[i, 6] = SeedTimestamp;
            }
            return grid;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DashboardWidgetPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WidgetKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardWidgetPreferences", x => x.Id);
                    table.CheckConstraint("CK_DashboardWidgetPreferences_RoleXorUserId", "([Role] IS NOT NULL AND [UserId] IS NULL) OR ([Role] IS NULL AND [UserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DashboardWidgetPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardWidgetPreferences_Role_WidgetKey",
                table: "DashboardWidgetPreferences",
                columns: new[] { "Role", "WidgetKey" },
                unique: true,
                filter: "[UserId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardWidgetPreferences_UserId_WidgetKey",
                table: "DashboardWidgetPreferences",
                columns: new[] { "UserId", "WidgetKey" },
                unique: true,
                filter: "[Role] IS NULL");

            // Role-default dashboard layouts — DashboardRoleDefaults.cs (Domain/Constants) is the
            // single source of truth this reads from, so the seeded rows and the documented
            // per-role widget table never drift apart.
            migrationBuilder.InsertData("DashboardWidgetPreferences", SeedColumns, BuildGrid(DashboardRoleDefaults.Admin, nameof(UserRole.Admin)));
            migrationBuilder.InsertData("DashboardWidgetPreferences", SeedColumns, BuildGrid(DashboardRoleDefaults.User, nameof(UserRole.User)));
            migrationBuilder.InsertData("DashboardWidgetPreferences", SeedColumns, BuildGrid(DashboardRoleDefaults.Reviewer, nameof(UserRole.Reviewer)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardWidgetPreferences");
        }
    }
}
