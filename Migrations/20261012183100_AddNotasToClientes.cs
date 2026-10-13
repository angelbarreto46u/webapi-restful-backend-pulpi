using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations;

public partial class AddNotasToClientes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "Notas", table: "Clientes", type: "nvarchar(500)", maxLength: 500,
        nullable: false, defaultValue: "");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "Notas", table: "Clientes");
}
