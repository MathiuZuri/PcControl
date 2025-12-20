using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHistorialTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuraciones",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuraciones", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Stock = table.Column<int>(type: "INTEGER", nullable: false),
                    EsServicio = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sesiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComputadoraId = table.Column<int>(type: "INTEGER", nullable: false),
                    HoraInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HoraFin = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalCalculado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCobrado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstaPagado = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetodoPago = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sesiones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SesionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DetallesVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreProductoSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Configuraciones",
                columns: new[] { "Clave", "Valor" },
                values: new object[,]
                {
                    { "NombreCiber", "Mi Ciber Lan" },
                    { "PrecioHora", "2.50" }
                });

            migrationBuilder.InsertData(
                table: "Productos",
                columns: new[] { "Id", "Categoria", "EsServicio", "Nombre", "Precio", "Stock" },
                values: new object[,]
                {
                    { 1, "Bebidas", false, "Coca Cola 500ml", 3.50m, 24 },
                    { 2, "Servicios", true, "Impresión B/N", 0.50m, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_VentaId",
                table: "DetallesVenta",
                column: "VentaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configuraciones");

            migrationBuilder.DropTable(
                name: "DetallesVenta");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Sesiones");

            migrationBuilder.DropTable(
                name: "Ventas");
        }
    }
}
