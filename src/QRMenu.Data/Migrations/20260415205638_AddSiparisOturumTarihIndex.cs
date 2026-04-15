using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiparisOturumTarihIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Siparisler_OturumId",
                table: "Siparisler");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_OturumId_OlusturmaTarihi",
                table: "Siparisler",
                columns: new[] { "OturumId", "OlusturmaTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Siparisler_OturumId_OlusturmaTarihi",
                table: "Siparisler");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_OturumId",
                table: "Siparisler",
                column: "OturumId");
        }
    }
}
