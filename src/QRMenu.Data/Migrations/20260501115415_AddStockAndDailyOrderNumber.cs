using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAndDailyOrderNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SepetDetaylar_SepetId",
                table: "SepetDetaylar");

            migrationBuilder.AddColumn<bool>(
                name: "AdminManuelPasifMi",
                table: "Urunler",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StokAdet",
                table: "Urunler",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GunlukSiparisNo",
                table: "Siparisler",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SiparisGunu",
                table: "Siparisler",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "SiparisTarihi",
                table: "Siparisler",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "UrunVaryasyonId",
                table: "SiparisDetaylar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UrunVaryasyonId",
                table: "SepetDetaylar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UrunVaryasyonlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    Ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdEN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EkFiyat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StokAdet = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    AdminManuelPasifMi = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SiraNo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrunVaryasyonlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UrunVaryasyonlar_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                UPDATE "Siparisler"
                SET "SiparisTarihi" = "OlusturmaTarihi"
                WHERE "SiparisTarihi" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Siparisler"
                SET "SiparisGunu" = date_trunc('day', timezone('Europe/Istanbul', "SiparisTarihi")) AT TIME ZONE 'UTC'
                WHERE "SiparisGunu" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);

            migrationBuilder.Sql(
                """
                WITH sirali AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "SiparisGunu"
                               ORDER BY "SiparisTarihi", "Id"
                           ) AS gunluk_no
                    FROM "Siparisler"
                )
                UPDATE "Siparisler" AS s
                SET "GunlukSiparisNo" = sirali.gunluk_no
                FROM sirali
                WHERE s."Id" = sirali."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_AktifMi_StokAdet",
                table: "Urunler",
                columns: new[] { "AktifMi", "StokAdet" });

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_SiparisTarihi",
                table: "Siparisler",
                column: "SiparisTarihi");

            migrationBuilder.CreateIndex(
                name: "UX_Siparisler_SiparisGunu_GunlukSiparisNo",
                table: "Siparisler",
                columns: new[] { "SiparisGunu", "GunlukSiparisNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiparisDetaylar_UrunVaryasyonId",
                table: "SiparisDetaylar",
                column: "UrunVaryasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_SepetDetaylar_Sepet_Urun_Varyasyon",
                table: "SepetDetaylar",
                columns: new[] { "SepetId", "UrunId", "UrunVaryasyonId" });

            migrationBuilder.CreateIndex(
                name: "IX_SepetDetaylar_UrunVaryasyonId",
                table: "SepetDetaylar",
                column: "UrunVaryasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_UrunVaryasyonlar_Urun_Aktif_Stok",
                table: "UrunVaryasyonlar",
                columns: new[] { "UrunId", "AktifMi", "StokAdet" });

            migrationBuilder.AddForeignKey(
                name: "FK_SepetDetaylar_UrunVaryasyonlar_UrunVaryasyonId",
                table: "SepetDetaylar",
                column: "UrunVaryasyonId",
                principalTable: "UrunVaryasyonlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SiparisDetaylar_UrunVaryasyonlar_UrunVaryasyonId",
                table: "SiparisDetaylar",
                column: "UrunVaryasyonId",
                principalTable: "UrunVaryasyonlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SepetDetaylar_UrunVaryasyonlar_UrunVaryasyonId",
                table: "SepetDetaylar");

            migrationBuilder.DropForeignKey(
                name: "FK_SiparisDetaylar_UrunVaryasyonlar_UrunVaryasyonId",
                table: "SiparisDetaylar");

            migrationBuilder.DropTable(
                name: "UrunVaryasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_AktifMi_StokAdet",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_Siparisler_SiparisTarihi",
                table: "Siparisler");

            migrationBuilder.DropIndex(
                name: "UX_Siparisler_SiparisGunu_GunlukSiparisNo",
                table: "Siparisler");

            migrationBuilder.DropIndex(
                name: "IX_SiparisDetaylar_UrunVaryasyonId",
                table: "SiparisDetaylar");

            migrationBuilder.DropIndex(
                name: "IX_SepetDetaylar_Sepet_Urun_Varyasyon",
                table: "SepetDetaylar");

            migrationBuilder.DropIndex(
                name: "IX_SepetDetaylar_UrunVaryasyonId",
                table: "SepetDetaylar");

            migrationBuilder.DropColumn(
                name: "AdminManuelPasifMi",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "StokAdet",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "GunlukSiparisNo",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "SiparisGunu",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "SiparisTarihi",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "UrunVaryasyonId",
                table: "SiparisDetaylar");

            migrationBuilder.DropColumn(
                name: "UrunVaryasyonId",
                table: "SepetDetaylar");

            migrationBuilder.CreateIndex(
                name: "IX_SepetDetaylar_SepetId",
                table: "SepetDetaylar",
                column: "SepetId");
        }
    }
}
