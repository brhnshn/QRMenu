using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityLogExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "SecurityLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestBody",
                table: "SecurityLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "SecurityLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TableId",
                table: "SecurityLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "SecurityLogs");

            migrationBuilder.DropColumn(
                name: "RequestBody",
                table: "SecurityLogs");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "SecurityLogs");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "SecurityLogs");
        }
    }
}
