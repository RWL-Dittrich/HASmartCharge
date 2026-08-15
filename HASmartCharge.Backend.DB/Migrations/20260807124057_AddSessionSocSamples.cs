using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionSocSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EndSocPercent",
                table: "ChargeSessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StartSocPercent",
                table: "ChargeSessions",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndSocPercent",
                table: "ChargeSessions");

            migrationBuilder.DropColumn(
                name: "StartSocPercent",
                table: "ChargeSessions");
        }
    }
}
