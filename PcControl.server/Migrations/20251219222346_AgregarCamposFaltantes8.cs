using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposFaltantes8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GastoId",
                table: "Gastos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_GastoId",
                table: "Gastos",
                column: "GastoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gastos_Gastos_GastoId",
                table: "Gastos",
                column: "GastoId",
                principalTable: "Gastos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gastos_Gastos_GastoId",
                table: "Gastos");

            migrationBuilder.DropIndex(
                name: "IX_Gastos_GastoId",
                table: "Gastos");

            migrationBuilder.DropColumn(
                name: "GastoId",
                table: "Gastos");
        }
    }
}
