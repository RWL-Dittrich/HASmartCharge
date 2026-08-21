using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddZaptecChargerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargerType",
                table: "ChargerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ZaptecChargerId",
                table: "ChargerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ZaptecPassword",
                table: "ChargerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ZaptecPollSeconds",
                table: "ChargerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ZaptecUsername",
                table: "ChargerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChargeControlMode",
                table: "CarSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "CarSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "ChargeControlMode",
                value: "HomeAssistant");

            migrationBuilder.UpdateData(
                table: "ChargerSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ChargerType", "ZaptecChargerId", "ZaptecPassword", "ZaptecPollSeconds", "ZaptecUsername" },
                values: new object[] { "Ocpp", "", "", 30, "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargerType",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ZaptecChargerId",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ZaptecPassword",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ZaptecPollSeconds",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ZaptecUsername",
                table: "ChargerSettings");

            migrationBuilder.DropColumn(
                name: "ChargeControlMode",
                table: "CarSettings");
        }
    }
}
