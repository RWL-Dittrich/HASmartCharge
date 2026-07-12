using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class ChargingDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BatteryCapacityKwh = table.Column<double>(type: "REAL", nullable: false),
                    TargetSocPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargeEfficiency = table.Column<double>(type: "REAL", nullable: false),
                    HaSocEntityId = table.Column<string>(type: "TEXT", nullable: false),
                    HaStartDomain = table.Column<string>(type: "TEXT", nullable: false),
                    HaStartService = table.Column<string>(type: "TEXT", nullable: false),
                    HaStartDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    HaStopDomain = table.Column<string>(type: "TEXT", nullable: false),
                    HaStopService = table.Column<string>(type: "TEXT", nullable: false),
                    HaStopDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    HaPluggedInEntityId = table.Column<string>(type: "TEXT", nullable: true),
                    HaChargingStateEntityId = table.Column<string>(type: "TEXT", nullable: true),
                    HaTargetSocEntityId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeadlineUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TargetSocPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    StartSocPercent = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedEnergyKwh = table.Column<double>(type: "REAL", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    SelectedHoursJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargePlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChargePointId = table.Column<string>(type: "TEXT", nullable: false),
                    FriendlyName = table.Column<string>(type: "TEXT", nullable: false),
                    MaxChargeKw = table.Column<double>(type: "REAL", nullable: false),
                    ConnectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    HeartbeatInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    MeterValueSampleInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    ClockAlignedDataInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    MeterValuesSampledData = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargerSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HourlyPrices",
                columns: table => new
                {
                    HourStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PricePerKwh = table.Column<decimal>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourlyPrices", x => x.HourStartUtc);
                });

            migrationBuilder.CreateTable(
                name: "PriceProviderSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApiUrl = table.Column<string>(type: "TEXT", nullable: false),
                    SupplierSlug = table.Column<string>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceProviderSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargeSessions",
                columns: table => new
                {
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargePointId = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MeterStartWh = table.Column<int>(type: "INTEGER", nullable: false),
                    MeterStopWh = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalKwh = table.Column<double>(type: "REAL", nullable: false),
                    TotalCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeSessions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_ChargeSessions_ChargePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "ChargePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HourlyEnergyUsage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    HourStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EnergyKwh = table.Column<double>(type: "REAL", nullable: false),
                    PricePerKwh = table.Column<decimal>(type: "TEXT", nullable: false),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourlyEnergyUsage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HourlyEnergyUsage_ChargeSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChargeSessions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CarSettings",
                columns: new[] { "Id", "BatteryCapacityKwh", "ChargeEfficiency", "HaChargingStateEntityId", "HaPluggedInEntityId", "HaSocEntityId", "HaStartDataJson", "HaStartDomain", "HaStartService", "HaStopDataJson", "HaStopDomain", "HaStopService", "HaTargetSocEntityId", "Name", "TargetSocPercent" },
                values: new object[] { 1, 75.0, 0.90000000000000002, null, null, "", null, "", "", null, "", "", null, "My EV", 100 });

            migrationBuilder.InsertData(
                table: "ChargerSettings",
                columns: new[] { "Id", "ChargePointId", "ClockAlignedDataInterval", "ConnectorId", "FriendlyName", "HeartbeatInterval", "MaxChargeKw", "MeterValueSampleInterval", "MeterValuesSampledData" },
                values: new object[] { 1, "", 10, 1, "Charger", 60, 11.0, 10, "Power.Active.Import,Energy.Active.Import.Register,Current.Import,Voltage,Current.Offered,Power.Offered,SoC,Voltage.L1,Voltage.L2,Voltage.L3" });

            migrationBuilder.InsertData(
                table: "PriceProviderSettings",
                columns: new[] { "Id", "ApiUrl", "Currency", "RefreshMinutes", "SupplierSlug" },
                values: new object[] { 1, "https://epexprijzen.nl/api/v1/prices/nextenergy/hourly", "EUR", 60, "nextenergy" });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeSessions_PlanId",
                table: "ChargeSessions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_HourlyEnergyUsage_SessionId",
                table: "HourlyEnergyUsage",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarSettings");

            migrationBuilder.DropTable(
                name: "ChargerSettings");

            migrationBuilder.DropTable(
                name: "HourlyEnergyUsage");

            migrationBuilder.DropTable(
                name: "HourlyPrices");

            migrationBuilder.DropTable(
                name: "PriceProviderSettings");

            migrationBuilder.DropTable(
                name: "ChargeSessions");

            migrationBuilder.DropTable(
                name: "ChargePlans");
        }
    }
}
