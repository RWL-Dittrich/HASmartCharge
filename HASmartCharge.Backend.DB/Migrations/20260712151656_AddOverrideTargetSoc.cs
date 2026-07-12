using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddOverrideTargetSoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetSocPercent",
                table: "ScheduleOverrides",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetSocPercent",
                table: "ScheduleOverrides");
        }
    }
}
