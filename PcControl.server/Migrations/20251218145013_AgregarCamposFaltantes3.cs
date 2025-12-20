using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposFaltantes3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialSesiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PcNombre = table.Column<string>(type: "TEXT", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImporteTiempo = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImporteExtra = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalCobrado = table.Column<decimal>(type: "TEXT", nullable: false),
                    MetodoPago = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialSesiones", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialSesiones");
        }
    }
}
