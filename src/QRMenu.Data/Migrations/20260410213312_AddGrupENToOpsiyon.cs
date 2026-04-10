using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupENToOpsiyon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GrupEN",
                table: "Opsiyonlar",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 1,
                column: "GrupEN",
                value: null);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 2,
                column: "GrupEN",
                value: null);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 3,
                column: "GrupEN",
                value: null);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 4,
                column: "GrupEN",
                value: null);

            migrationBuilder.UpdateData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 5,
                column: "GrupEN",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrupEN",
                table: "Opsiyonlar");
        }
    }
}
