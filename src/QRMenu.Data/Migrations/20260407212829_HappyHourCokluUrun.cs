using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class HappyHourCokluUrun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HappyHourUrunler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HappyHourId = table.Column<int>(type: "integer", nullable: false),
                    UrunId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HappyHourUrunler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HappyHourUrunler_HappyHour_HappyHourId",
                        column: x => x.HappyHourId,
                        principalTable: "HappyHour",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HappyHourUrunler_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HappyHourUrunler_HappyHourId_UrunId",
                table: "HappyHourUrunler",
                columns: new[] { "HappyHourId", "UrunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HappyHourUrunler_UrunId",
                table: "HappyHourUrunler",
                column: "UrunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HappyHourUrunler");
        }
    }
}
