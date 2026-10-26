using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMasaQrToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrToken",
                table: "Masalar",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 1,
                column: "QrToken",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 2,
                column: "QrToken",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 3,
                column: "QrToken",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 4,
                column: "QrToken",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 5,
                column: "QrToken",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Masalar_QrToken",
                table: "Masalar",
                column: "QrToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Masalar_QrToken",
                table: "Masalar");

            migrationBuilder.DropColumn(
                name: "QrToken",
                table: "Masalar");
        }
    }
}
