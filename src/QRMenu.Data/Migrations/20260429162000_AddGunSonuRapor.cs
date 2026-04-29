using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using QRMenu.Data.Data;

#nullable disable

namespace QRMenu.Data.Migrations
{
    [DbContext(typeof(QRMenuDbContext))]
    [Migration("20260429162000_AddGunSonuRapor")]
    public partial class AddGunSonuRapor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GunSonuRaporlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ToplamCiro = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SiparisSayisi = table.Column<int>(type: "integer", nullable: false),
                    OdemeTipleriJson = table.Column<string>(type: "text", nullable: false),
                    KapanisTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KapatanKullaniciId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GunSonuRaporlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GunSonuRaporlari_Tarih",
                table: "GunSonuRaporlari",
                column: "Tarih",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GunSonuRaporlari");
        }
    }
}
