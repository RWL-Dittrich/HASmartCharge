using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class ChargePowerAmpsConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhaseCount",
                table: "ChargerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "SupplyVoltage",
                table: "ChargerSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.UpdateData(
                table: "ChargerSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PhaseCount", "SupplyVoltage" },
                values: new object[] { 3, 230.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhaseCount",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "SupplyVoltage",
                table: "ChargerSettings");
        }
    }
}
