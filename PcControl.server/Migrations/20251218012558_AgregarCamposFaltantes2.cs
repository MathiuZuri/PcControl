using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposFaltantes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropColumn(
                name: "EsServicio",
                table: "Productos");

            migrationBuilder.AlterColumn<decimal>(
                name: "Precio",
                table: "Productos",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.CreateTable(
                name: "Tarifas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Minutos = table.Column<int>(type: "INTEGER", nullable: false),
                    Precio = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarifas", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tarifas");

            migrationBuilder.AlterColumn<decimal>(
                name: "Precio",
                table: "Productos",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<bool>(
                name: "EsServicio",
                table: "Productos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "Productos",
                columns: new[] { "Id", "Categoria", "EsServicio", "Nombre", "Precio", "Stock" },
                values: new object[,]
                {
                    { 1, "Bebidas", false, "Coca Cola 500ml", 3.50m, 24 },
                    { 2, "Servicios", true, "Impresión B/N", 0.50m, 0 }
                });
        }
    }
}
