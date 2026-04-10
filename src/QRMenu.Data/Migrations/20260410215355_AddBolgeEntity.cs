using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBolgeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BolgeId",
                table: "Masalar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bolgeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    SiraNo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bolgeler", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 1,
                column: "BolgeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 2,
                column: "BolgeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 3,
                column: "BolgeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 4,
                column: "BolgeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masalar",
                keyColumn: "Id",
                keyValue: 5,
                column: "BolgeId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Masalar_BolgeId",
                table: "Masalar",
                column: "BolgeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Masalar_Bolgeler_BolgeId",
                table: "Masalar",
                column: "BolgeId",
                principalTable: "Bolgeler",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Masalar_Bolgeler_BolgeId",
                table: "Masalar");

            migrationBuilder.DropTable(
                name: "Bolgeler");

            migrationBuilder.DropIndex(
                name: "IX_Masalar_BolgeId",
                table: "Masalar");

            migrationBuilder.DropColumn(
                name: "BolgeId",
                table: "Masalar");
        }
    }
}
