using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations
{
    /// <inheritdoc />
    public partial class ThirdCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Email", "FechaRegistro", "NombreCompleto", "PasswordHash", "Rol", "Username" },
                values: new object[] { 1, "admin@pulpi.com", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Administrador Pulpi", "$2a$11$fviDAosITtmkFY31wK6rS.Nkt9vVRYci6O9IiB6citji1gn8a6AEW", "Administrador", "admin_pulpi" });
        }
    }
}
