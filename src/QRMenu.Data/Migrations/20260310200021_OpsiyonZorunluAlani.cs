using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class OpsiyonZorunluAlani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Zorunlu",
                table: "Opsiyonlar",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 1,
                column: "Zorunlu",
                value: true);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 2,
                column: "Zorunlu",
                value: true);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 3,
                column: "Zorunlu",
                value: true);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 4,
                column: "Zorunlu",
                value: true);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 5,
                column: "Zorunlu",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Zorunlu",
                table: "Opsiyonlar");
        }
    }
}
