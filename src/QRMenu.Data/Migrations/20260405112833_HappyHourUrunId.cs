using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class HappyHourUrunId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UrunId",
                table: "HappyHour",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HappyHour_UrunId",
                table: "HappyHour",
                column: "UrunId");

            migrationBuilder.AddForeignKey(
                name: "FK_HappyHour_Urunler_UrunId",
                table: "HappyHour",
                column: "UrunId",
                principalTable: "Urunler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HappyHour_Urunler_UrunId",
                table: "HappyHour");

            migrationBuilder.DropIndex(
                name: "IX_HappyHour_UrunId",
                table: "HappyHour");

            migrationBuilder.DropColumn(
                name: "UrunId",
                table: "HappyHour");
        }
    }
}
