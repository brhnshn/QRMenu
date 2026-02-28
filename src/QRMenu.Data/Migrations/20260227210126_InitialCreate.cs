using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLoglar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TabloAdi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KayitId = table.Column<int>(type: "integer", nullable: false),
                    Islem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EskiDeger = table.Column<string>(type: "text", nullable: true),
                    YeniDeger = table.Column<string>(type: "text", nullable: true),
                    KullaniciId = table.Column<int>(type: "integer", nullable: true),
                    IslemTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLoglar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kategoriler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdEN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SiraNo = table.Column<int>(type: "integer", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategoriler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KullaniciAdi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AdSoyad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SifreHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Masalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasaNo = table.Column<int>(type: "integer", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    QrKodUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Masalar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Opsiyonlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdEN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Grup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EkFiyat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opsiyonlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Urunler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KategoriId = table.Column<int>(type: "integer", nullable: false),
                    Ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AdEN = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AciklamaEN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Fiyat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GorselUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    PopulerMi = table.Column<bool>(type: "boolean", nullable: false),
                    SatisSayisi = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Urunler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Urunler_Kategoriler_KategoriId",
                        column: x => x.KategoriId,
                        principalTable: "Kategoriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Oturumlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasaId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kullanildi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SonIslemTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oturumlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Oturumlar_Masalar_MasaId",
                        column: x => x.MasaId,
                        principalTable: "Masalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UrunOpsiyonlar",
                columns: table => new
                {
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    OpsiyonId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrunOpsiyonlar", x => new { x.UrunId, x.OpsiyonId });
                    table.ForeignKey(
                        name: "FK_UrunOpsiyonlar_Opsiyonlar_OpsiyonId",
                        column: x => x.OpsiyonId,
                        principalTable: "Opsiyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UrunOpsiyonlar_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sepetler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OturumId = table.Column<int>(type: "integer", nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sepetler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sepetler_Oturumlar_OturumId",
                        column: x => x.OturumId,
                        principalTable: "Oturumlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Siparisler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasaId = table.Column<int>(type: "integer", nullable: false),
                    OturumId = table.Column<int>(type: "integer", nullable: true),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ToplamTutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notlar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Siparisler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Siparisler_Masalar_MasaId",
                        column: x => x.MasaId,
                        principalTable: "Masalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_Oturumlar_OturumId",
                        column: x => x.OturumId,
                        principalTable: "Oturumlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SepetDetaylar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SepetId = table.Column<int>(type: "integer", nullable: false),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    Adet = table.Column<int>(type: "integer", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SeciliOpsiyonlar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SepetDetaylar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SepetDetaylar_Sepetler_SepetId",
                        column: x => x.SepetId,
                        principalTable: "Sepetler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SepetDetaylar_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Odemeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiparisId = table.Column<int>(type: "integer", nullable: false),
                    Tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OdemeTipi = table.Column<int>(type: "integer", nullable: false),
                    OdemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Odemeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Odemeler_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiparisDetaylar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiparisId = table.Column<int>(type: "integer", nullable: false),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    Adet = table.Column<int>(type: "integer", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SeciliOpsiyonlar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Durum = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiparisDetaylar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiparisDetaylar_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SiparisDetaylar_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Kategoriler",
                columns: new[] { "Id", "Ad", "AdEN", "AktifMi", "SiraNo" },
                values: new object[,]
                {
                    { 1, "Sıcak İçecekler", "Hot Beverages", true, 1 },
                    { 2, "Soğuk İçecekler", "Cold Beverages", true, 2 },
                    { 3, "Atıştırmalıklar", "Snacks", true, 3 }
                });

            migrationBuilder.InsertData(
                table: "Masalar",
                columns: new[] { "Id", "AktifMi", "MasaNo", "QrKodUrl" },
                values: new object[,]
                {
                    { 1, true, 1, null },
                    { 2, true, 2, null },
                    { 3, true, 3, null },
                    { 4, true, 4, null },
                    { 5, true, 5, null }
                });

            migrationBuilder.InsertData(
                table: "Opsiyonlar",
                columns: new[] { "Id", "Ad", "AdEN", "EkFiyat", "Grup" },
                values: new object[,]
                {
                    { 1, "Küçük", "Small", 0m, "Boyut" },
                    { 2, "Orta", "Medium", 10.00m, "Boyut" },
                    { 3, "Büyük", "Large", 20.00m, "Boyut" },
                    { 4, "Tam Yağlı Süt", "Whole Milk", 0m, "Süt Tipi" },
                    { 5, "Yağsız Süt", "Skim Milk", 0m, "Süt Tipi" }
                });

            migrationBuilder.InsertData(
                table: "Urunler",
                columns: new[] { "Id", "Aciklama", "AciklamaEN", "Ad", "AdEN", "AktifMi", "Fiyat", "GorselUrl", "KategoriId", "PopulerMi", "SatisSayisi" },
                values: new object[,]
                {
                    { 1, "Taze demlenmiş filtre kahve", "Freshly brewed filter coffee", "Filtre Kahve", "Filter Coffee", true, 45.00m, null, 1, true, 150 },
                    { 2, "Espresso ve sütlü kahve", "Espresso with steamed milk", "Latte", "Latte", true, 65.00m, null, 1, true, 200 },
                    { 3, "Espresso ve sıcak su", "Espresso with hot water", "Americano", "Americano", true, 50.00m, null, 1, false, 80 },
                    { 4, "Geleneksel Türk kahvesi", "Traditional Turkish coffee", "Türk Kahvesi", "Turkish Coffee", true, 40.00m, null, 1, false, 120 },
                    { 5, "Soğuk süt ve espresso", "Cold milk and espresso", "Buzlu Latte", "Iced Latte", true, 70.00m, null, 2, true, 180 },
                    { 6, "Taze sıkılmış limonata", "Freshly squeezed lemonade", "Limonata", "Lemonade", true, 55.00m, null, 2, false, 90 },
                    { 7, "Karışık meyveli smoothie", "Mixed fruit smoothie", "Smoothie", "Smoothie", false, 75.00m, null, 2, false, 60 },
                    { 8, "New York usulü cheesecake", "New York style cheesecake", "Cheesecake", "Cheesecake", true, 90.00m, null, 3, true, 160 },
                    { 9, "Çikolatalı brownie", "Chocolate brownie", "Brownie", "Brownie", true, 70.00m, null, 3, false, 95 },
                    { 10, "Tavuklu kulüp sandviç", "Chicken club sandwich", "Sandviç", "Sandwich", true, 110.00m, null, 3, false, 70 }
                });

            migrationBuilder.InsertData(
                table: "UrunOpsiyonlar",
                columns: new[] { "OpsiyonId", "UrunId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 2 },
                    { 4, 2 },
                    { 5, 2 },
                    { 1, 3 },
                    { 2, 3 },
                    { 3, 3 },
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 4, 5 },
                    { 5, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KullaniciAdi",
                table: "Kullanicilar",
                column: "KullaniciAdi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Masalar_MasaNo",
                table: "Masalar",
                column: "MasaNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Odemeler_SiparisId",
                table: "Odemeler",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_Oturumlar_AktifMi",
                table: "Oturumlar",
                column: "AktifMi");

            migrationBuilder.CreateIndex(
                name: "IX_Oturumlar_MasaId",
                table: "Oturumlar",
                column: "MasaId");

            migrationBuilder.CreateIndex(
                name: "IX_Oturumlar_TokenHash",
                table: "Oturumlar",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_SepetDetaylar_SepetId",
                table: "SepetDetaylar",
                column: "SepetId");

            migrationBuilder.CreateIndex(
                name: "IX_SepetDetaylar_UrunId",
                table: "SepetDetaylar",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Sepetler_OturumId",
                table: "Sepetler",
                column: "OturumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiparisDetaylar_SiparisId",
                table: "SiparisDetaylar",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisDetaylar_UrunId",
                table: "SiparisDetaylar",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_Durum",
                table: "Siparisler",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_MasaId",
                table: "Siparisler",
                column: "MasaId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_OturumId",
                table: "Siparisler",
                column: "OturumId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_AktifMi",
                table: "Urunler",
                column: "AktifMi");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_KategoriId",
                table: "Urunler",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_UrunOpsiyonlar_OpsiyonId",
                table: "UrunOpsiyonlar",
                column: "OpsiyonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLoglar");

            migrationBuilder.DropTable(
                name: "Kullanicilar");

            migrationBuilder.DropTable(
                name: "Odemeler");

            migrationBuilder.DropTable(
                name: "SepetDetaylar");

            migrationBuilder.DropTable(
                name: "SiparisDetaylar");

            migrationBuilder.DropTable(
                name: "UrunOpsiyonlar");

            migrationBuilder.DropTable(
                name: "Sepetler");

            migrationBuilder.DropTable(
                name: "Siparisler");

            migrationBuilder.DropTable(
                name: "Opsiyonlar");

            migrationBuilder.DropTable(
                name: "Urunler");

            migrationBuilder.DropTable(
                name: "Oturumlar");

            migrationBuilder.DropTable(
                name: "Kategoriler");

            migrationBuilder.DropTable(
                name: "Masalar");
        }
    }
}
