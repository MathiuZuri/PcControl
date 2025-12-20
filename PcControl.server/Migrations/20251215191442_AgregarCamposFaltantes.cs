using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposFaltantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TarifaPorHora",
                table: "Computadoras",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<decimal>(
                name: "ImporteExtra",
                table: "Computadoras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "NotaAdmin",
                table: "Computadoras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TiempoLimiteMinutos",
                table: "Computadoras",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Computadoras",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ImporteExtra", "NotaAdmin", "TiempoLimiteMinutos" },
                values: new object[] { 0m, null, null });

            migrationBuilder.UpdateData(
                table: "Computadoras",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ImporteExtra", "NotaAdmin", "TiempoLimiteMinutos" },
                values: new object[] { 0m, null, null });

            migrationBuilder.UpdateData(
                table: "Computadoras",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ImporteExtra", "NotaAdmin", "TiempoLimiteMinutos" },
                values: new object[] { 0m, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImporteExtra",
                table: "Computadoras");

            migrationBuilder.DropColumn(
                name: "NotaAdmin",
                table: "Computadoras");

            migrationBuilder.DropColumn(
                name: "TiempoLimiteMinutos",
                table: "Computadoras");

            migrationBuilder.AlterColumn<decimal>(
                name: "TarifaPorHora",
                table: "Computadoras",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }
    }
}
