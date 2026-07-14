using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HASmartCharge.Backend.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddMqttSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MqttSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Password = table.Column<string>(type: "TEXT", nullable: true),
                    UseTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    BaseTopic = table.Column<string>(type: "TEXT", nullable: false),
                    DiscoveryPrefix = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MqttSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MqttSettings",
                columns: new[] { "Id", "BaseTopic", "ClientId", "DiscoveryPrefix", "Enabled", "Host", "Password", "Port", "UseTls", "Username" },
                values: new object[] { 1, "hasmartcharge", "hasmartcharge", "homeassistant", false, "core-mosquitto", null, 1883, false, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MqttSettings");
        }
    }
}
