using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiparisDetayPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SiparisDetaylar_SiparisId_Durum",
                table: "SiparisDetaylar",
                columns: new[] { "SiparisId", "Durum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SiparisDetaylar_SiparisId_Durum",
                table: "SiparisDetaylar");
        }
    }
}
