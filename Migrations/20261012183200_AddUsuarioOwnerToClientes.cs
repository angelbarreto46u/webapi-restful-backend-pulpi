using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations;

public partial class AddUsuarioOwnerToClientes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "UsuarioId", table: "Clientes", type: "int", nullable: true);

        // Los clientes existentes pasan a pertenecer al administrador (Id = 1).
        migrationBuilder.Sql("UPDATE [Clientes] SET [UsuarioId] = 1 WHERE [UsuarioId] IS NULL;");
        // Evita que transacciones antiguas de otra cuenta sigan vinculadas a clientes del administrador.
        migrationBuilder.Sql("UPDATE t SET [ClienteId] = NULL FROM [Transacciones] AS t INNER JOIN [Clientes] AS c ON t.[ClienteId] = c.[Id] WHERE t.[UsuarioId] <> c.[UsuarioId];");

        migrationBuilder.AlterColumn<int>(
            name: "UsuarioId", table: "Clientes", type: "int", nullable: false,
            oldClrType: typeof(int), oldType: "int", oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Clientes_UsuarioId", table: "Clientes", column: "UsuarioId");

        migrationBuilder.AddForeignKey(
            name: "FK_Clientes_Usuarios_UsuarioId", table: "Clientes", column: "UsuarioId",
            principalTable: "Usuarios", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Clientes_Usuarios_UsuarioId", table: "Clientes");
        migrationBuilder.DropIndex(name: "IX_Clientes_UsuarioId", table: "Clientes");
        migrationBuilder.DropColumn(name: "UsuarioId", table: "Clientes");
    }
}
