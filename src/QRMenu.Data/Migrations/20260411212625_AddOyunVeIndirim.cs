using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOyunVeIndirim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OyunOynandiMi",
                table: "Siparisler",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "KazanilanIndirimler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiparisId = table.Column<int>(type: "integer", nullable: false),
                    OdulTanim = table.Column<string>(type: "text", nullable: false),
                    UgulananIndirimTutari = table.Column<decimal>(type: "numeric", nullable: false),
                    KazanmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KazanilanIndirimler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KazanilanIndirimler_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OyunAyarlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    Tip = table.Column<string>(type: "text", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OyunAyarlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OyunOduller",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OyunAyarId = table.Column<int>(type: "integer", nullable: false),
                    OdulTanim = table.Column<string>(type: "text", nullable: false),
                    IndirimYuzdesi = table.Column<decimal>(type: "numeric", nullable: false),
                    IndirimTutari = table.Column<decimal>(type: "numeric", nullable: false),
                    IhtimalYuzdesi = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OyunOduller", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OyunOduller_OyunAyarlar_OyunAyarId",
                        column: x => x.OyunAyarId,
                        principalTable: "OyunAyarlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KazanilanIndirimler_SiparisId",
                table: "KazanilanIndirimler",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_OyunOduller_OyunAyarId",
                table: "OyunOduller",
                column: "OyunAyarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KazanilanIndirimler");

            migrationBuilder.DropTable(
                name: "OyunOduller");

            migrationBuilder.DropTable(
                name: "OyunAyarlar");

            migrationBuilder.DropColumn(
                name: "OyunOynandiMi",
                table: "Siparisler");
        }
    }
}
