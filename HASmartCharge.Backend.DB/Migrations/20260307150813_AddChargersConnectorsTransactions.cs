using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddChargersConnectorsTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chargers",
                columns: table => new
                {
                    ChargePointId = table.Column<string>(type: "TEXT", nullable: false),
                    Vendor = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastBootNotificationAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chargers", x => x.ChargePointId);
                });

            migrationBuilder.CreateTable(
                name: "ChargingTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChargePointId = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    IdTag = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MeterStartWh = table.Column<int>(type: "INTEGER", nullable: false),
                    StopTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MeterStopWh = table.Column<int>(type: "INTEGER", nullable: true),
                    StopReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargingTransactions_Chargers_ChargePointId",
                        column: x => x.ChargePointId,
                        principalTable: "Chargers",
                        principalColumn: "ChargePointId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Connectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChargePointId = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastStatus = table.Column<string>(type: "TEXT", nullable: true),
                    LastErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastStatusUpdateAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connectors_Chargers_ChargePointId",
                        column: x => x.ChargePointId,
                        principalTable: "Chargers",
                        principalColumn: "ChargePointId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingTransactions_ChargePointId_ConnectorId",
                table: "ChargingTransactions",
                columns: new[] { "ChargePointId", "ConnectorId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingTransactions_IdTag",
                table: "ChargingTransactions",
                column: "IdTag");

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_ChargePointId_ConnectorId",
                table: "Connectors",
                columns: new[] { "ChargePointId", "ConnectorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargingTransactions");

            migrationBuilder.DropTable(
                name: "Connectors");

            migrationBuilder.DropTable(
                name: "Chargers");
        }
    }
}
