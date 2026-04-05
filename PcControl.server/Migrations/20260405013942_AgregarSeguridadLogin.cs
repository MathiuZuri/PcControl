using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcControl.server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSeguridadLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$J4v1fjyQQZw6GSt2TJRLTuShkxGFkItygUUMGcd0jaEbcC9uB1coC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "admin123");
        }
    }
}
