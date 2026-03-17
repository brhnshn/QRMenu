using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKullaniciRolSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Kullanicilar",
                columns: new[] { "Id", "AdSoyad", "AktifMi", "KullaniciAdi", "Rol", "SifreHash" },
                values: new object[,]
                {
                    { 1, "Sistem Yöneticisi", true, "admin", 0, "123456" },
                    { 2, "Garson Test", true, "garson", 1, "123456" },
                    { 3, "Kasa Test", true, "kasa", 3, "123456" },
                    { 4, "Mutfak Test", true, "mutfak", 4, "123456" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}
