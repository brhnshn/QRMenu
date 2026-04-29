using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 3, 1 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 1, 2 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 4, 2 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 5, 2 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 2, 3 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 3, 3 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 1, 5 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 2, 5 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 3, 5 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 4, 5 });

            migrationBuilder.DeleteData(
                table: "UrunOpsiyonlar",
                keyColumns: new[] { "OpsiyonId", "UrunId" },
                keyValues: new object[] { 5, 5 });

            migrationBuilder.DeleteData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Opsiyonlar",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.CreateTable(
                name: "SecurityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    Path = table.Column<string>(type: "text", nullable: true),
                    Method = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityLogs", x => x.Id);
                });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityLogs");

            migrationBuilder.InsertData(
                table: "Opsiyonlar",
                columns: new[] { "Id", "Ad", "AdEN", "EkFiyat", "Grup", "GrupEN", "Zorunlu" },
                values: new object[,]
                {
                    { 1, "Küçük", "Small", 0m, "Boyut", null, true },
                    { 2, "Orta", "Medium", 10.00m, "Boyut", null, true },
                    { 3, "Büyük", "Large", 20.00m, "Boyut", null, true },
                    { 4, "Tam Yağlı Süt", "Whole Milk", 0m, "Süt Tipi", null, true },
                    { 5, "Yağsız Süt", "Skim Milk", 0m, "Süt Tipi", null, true }
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
        }
    }
}
