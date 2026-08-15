using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class ChargePowerConfigurationMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargePowerConfigurationKey",
                table: "ChargerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChargePowerConfigurationUnit",
                table: "ChargerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChargePowerControlMode",
                table: "ChargerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "ChargerSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ChargePowerConfigurationKey", "ChargePowerConfigurationUnit", "ChargePowerControlMode" },
                values: new object[] { "", "A", "ChargingProfile" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargePowerConfigurationKey",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ChargePowerConfigurationUnit",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ChargePowerControlMode",
                table: "ChargerSettings");
        }
    }
}
