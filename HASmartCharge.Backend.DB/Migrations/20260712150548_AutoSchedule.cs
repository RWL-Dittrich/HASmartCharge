using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class AutoSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoScheduleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", nullable: false),
                    WeeklyJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoScheduleSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateLocal = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DepartureLocal = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleOverrides", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AutoScheduleSettings",
                columns: new[] { "Id", "Enabled", "TimeZoneId", "WeeklyJson" },
                values: new object[] { 1, false, "Europe/Amsterdam", "[{\"dayOfWeek\":0,\"enabled\":false,\"departureLocal\":\"07:00\"},{\"dayOfWeek\":1,\"enabled\":false,\"departureLocal\":\"07:00\"},{\"dayOfWeek\":2,\"enabled\":false,\"departureLocal\":\"07:00\"},{\"dayOfWeek\":3,\"enabled\":false,\"departureLocal\":\"07:00\"},{\"dayOfWeek\":4,\"enabled\":false,\"departureLocal\":\"07:00\"},{\"dayOfWeek\":5,\"enabled\":false,\"departureLocal\":\"07:00\"},{\"dayOfWeek\":6,\"enabled\":false,\"departureLocal\":\"07:00\"}]" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOverrides_DateLocal",
                table: "ScheduleOverrides",
                column: "DateLocal",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoScheduleSettings");

            migrationBuilder.DropTable(
                name: "ScheduleOverrides");
        }
    }
}
