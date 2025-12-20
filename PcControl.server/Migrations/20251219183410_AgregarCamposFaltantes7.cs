using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposFaltantes7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InicioPausa",
                table: "Computadoras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Computadoras",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "InicioPausa", "Nombre" },
                values: new object[] { null, "PC-TEST" });

            migrationBuilder.UpdateData(
                table: "Computadoras",
                keyColumn: "Id",
                keyValue: 2,
                column: "InicioPausa",
                value: null);

            migrationBuilder.UpdateData(
                table: "Computadoras",
                keyColumn: "Id",
                keyValue: 3,
                column: "InicioPausa",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InicioPausa",
                table: "Computadoras");

            migrationBuilder.UpdateData(
                table: "Computadoras",
                keyColumn: "Id",
                keyValue: 1,
                column: "Nombre",
                value: "PC-01");
        }
    }
}
