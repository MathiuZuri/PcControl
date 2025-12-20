using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Computadoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", nullable: false),
                    HoraInicio = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HoraFin = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TarifaPorHora = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Computadoras", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Computadoras",
                columns: new[] { "Id", "Estado", "HoraFin", "HoraInicio", "IpAddress", "MacAddress", "Nombre", "TarifaPorHora" },
                values: new object[,]
                {
                    { 1, "Disponible", null, null, "", "", "PC-01", 2.50m },
                    { 2, "Disponible", null, null, "", "", "PC-02", 2.50m },
                    { 3, "Disponible", null, null, "", "", "PC-03", 2.50m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Computadoras");
        }
    }
}
