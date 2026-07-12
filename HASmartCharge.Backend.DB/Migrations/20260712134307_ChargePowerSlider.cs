using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class ChargePowerSlider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ChargePowerMaxKw",
                table: "ChargerSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ChargePowerMinKw",
                table: "ChargerSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ChargePowerSetpointKw",
                table: "ChargerSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.UpdateData(
                table: "ChargerSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ChargePowerMaxKw", "ChargePowerMinKw", "ChargePowerSetpointKw" },
                values: new object[] { 11.0, 1.3999999999999999, 11.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargePowerMaxKw",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ChargePowerMinKw",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ChargePowerSetpointKw",
                table: "ChargerSettings");
        }
    }
}
