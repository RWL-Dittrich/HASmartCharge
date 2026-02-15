using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartChargeBackend.DB.Migrations
{
    /// <inheritdoc />
    public partial class Initialmigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomeAssistantConnections",
                columns: table => new
                {
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    TokenType = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresIn = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeAssistantConnections", x => x.BaseUrl);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeAssistantConnections");
        }
    }
}
