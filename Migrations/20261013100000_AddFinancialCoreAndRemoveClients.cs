using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations;

/// <summary>
/// Normaliza roles y categorías, añade caja/auditoría y retira el módulo Clientes.
/// La retirada de Clientes es deliberadamente destructiva; exporte una copia de la base antes de aplicarla.
/// </summary>
public partial class AddFinancialCoreAndRemoveClients : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                IdRol = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Nombre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Descripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Roles", x => x.IdRol));

        migrationBuilder.InsertData("Roles", new[] { "IdRol", "Nombre", "Descripcion" }, new object[,]
        {
            { 1, "Administrador", "Acceso completo al sistema." },
            { 2, "Cajero", "Registra movimientos y administra su sesión de caja." }
        });

        migrationBuilder.AddColumn<int>(name: "IdRol", table: "Usuarios", type: "int", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "Activo", table: "Usuarios", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql(@"
UPDATE Usuarios
SET IdRol = CASE WHEN LOWER(LTRIM(RTRIM(Rol))) = 'administrador' THEN 1 ELSE 2 END;
UPDATE Usuarios SET Rol = 'Cajero' WHERE LOWER(LTRIM(RTRIM(Rol))) <> 'administrador';
");
        migrationBuilder.DropColumn(name: "Rol", table: "Usuarios");
        migrationBuilder.AlterColumn<int>(name: "IdRol", table: "Usuarios", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_Usuarios_IdRol", table: "Usuarios", column: "IdRol");
        migrationBuilder.CreateIndex(name: "IX_Roles_Nombre", table: "Roles", column: "Nombre", unique: true);
        migrationBuilder.AddForeignKey(name: "FK_Usuarios_Roles_IdRol", table: "Usuarios", column: "IdRol", principalTable: "Roles", principalColumn: "IdRol", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AlterColumn<string>(name: "PasswordHash", table: "Usuarios", type: "nvarchar(255)", maxLength: 255, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");

        migrationBuilder.CreateTable(
            name: "Categorias",
            columns: table => new
            {
                IdCategoria = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Descripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Activa = table.Column<bool>(type: "bit", nullable: false),
                FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Categorias", x => x.IdCategoria);
                table.CheckConstraint("CK_Categorias_Tipo", "[Tipo] IN ('Ingreso', 'Gasto')");
            });

        migrationBuilder.Sql(@"
INSERT INTO Categorias (Nombre, Tipo, Descripcion, Activa, FechaCreacion) VALUES
(N'Ventas del mostrador', N'Ingreso', N'Ingresos por ventas habituales.', 1, SYSUTCDATETIME()),
(N'Otros ingresos', N'Ingreso', N'Entradas de dinero no clasificadas como venta habitual.', 1, SYSUTCDATETIME()),
(N'Compra de mercancía', N'Gasto', N'Importe global por compra de mercancía.', 1, SYSUTCDATETIME()),
(N'Servicios básicos', N'Gasto', N'Agua, electricidad, internet y servicios similares.', 1, SYSUTCDATETIME()),
(N'Alquiler del local', N'Gasto', N'Arrendamiento del establecimiento.', 1, SYSUTCDATETIME()),
(N'Transporte', N'Gasto', N'Gastos de transporte relacionados con el negocio.', 1, SYSUTCDATETIME()),
(N'Mantenimiento y reparaciones', N'Gasto', N'Reparaciones del local y equipos.', 1, SYSUTCDATETIME()),
(N'Gastos administrativos', N'Gasto', N'Papelería, limpieza y otros gastos administrativos.', 1, SYSUTCDATETIME()),
(N'Pago de personal', N'Gasto', N'Salarios y pagos relacionados con el personal.', 1, SYSUTCDATETIME()),
(N'Otros egresos', N'Gasto', N'Salidas de dinero no clasificadas en otra categoría.', 1, SYSUTCDATETIME());

;WITH CategoriasAnteriores AS (
    SELECT DISTINCT
        COALESCE(NULLIF(LEFT(LTRIM(RTRIM(Categoria)), 60), N''), N'General') AS NombreNormalizado,
        CASE WHEN LOWER(LTRIM(RTRIM(Tipo))) = 'ingreso' THEN N'Ingreso' ELSE N'Gasto' END AS TipoNormalizado
    FROM Transacciones
)
INSERT INTO Categorias (Nombre, Tipo, Descripcion, Activa, FechaCreacion)
SELECT legacy.NombreNormalizado, legacy.TipoNormalizado, N'Categoría conservada de una transacción anterior.', 1, SYSUTCDATETIME()
FROM CategoriasAnteriores legacy
WHERE NOT EXISTS (
    SELECT 1 FROM Categorias c
    WHERE c.Tipo = legacy.TipoNormalizado AND LOWER(c.Nombre) = LOWER(legacy.NombreNormalizado)
);
");

        migrationBuilder.CreateTable(
            name: "SesionesCaja",
            columns: table => new
            {
                IdSesionCaja = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                IdUsuarioApertura = table.Column<int>(type: "int", nullable: false),
                FechaHoraApertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                SaldoInicial = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                IdUsuarioCierre = table.Column<int>(type: "int", nullable: true),
                FechaHoraCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                SaldoEsperado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                SaldoDeclarado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Diferencia = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Estado = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Observaciones = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SesionesCaja", x => x.IdSesionCaja);
                table.CheckConstraint("CK_SesionesCaja_Estado", "[Estado] IN ('ABIERTA', 'CERRADA')");
                table.CheckConstraint("CK_SesionesCaja_SaldoInicial", "[SaldoInicial] >= 0");
                table.ForeignKey("FK_SesionesCaja_Usuarios_IdUsuarioApertura", x => x.IdUsuarioApertura, "Usuarios", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SesionesCaja_Usuarios_IdUsuarioCierre", x => x.IdUsuarioCierre, "Usuarios", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AuditoriaAcciones",
            columns: table => new
            {
                IdAuditoria = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                IdUsuario = table.Column<int>(type: "int", nullable: true),
                Entidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                IdRegistro = table.Column<long>(type: "bigint", nullable: true),
                Accion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                DatosAnteriores = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DatosNuevos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditoriaAcciones", x => x.IdAuditoria);
                table.ForeignKey("FK_AuditoriaAcciones_Usuarios_IdUsuario", x => x.IdUsuario, "Usuarios", "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.AddColumn<int>(name: "IdCategoria", table: "Transacciones", type: "int", nullable: true);
        migrationBuilder.AddColumn<long>(name: "IdSesionCaja", table: "Transacciones", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Estado", table: "Transacciones", type: "nvarchar(12)", maxLength: 12, nullable: false, defaultValue: "REGISTRADA");
        migrationBuilder.AddColumn<DateTime>(name: "FechaRegistro", table: "Transacciones", type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()");
        migrationBuilder.RenameColumn(name: "Fecha", table: "Transacciones", newName: "FechaMovimiento");

        migrationBuilder.Sql(@"
UPDATE t
SET IdCategoria = c.IdCategoria
FROM Transacciones t
INNER JOIN Categorias c
    ON c.Tipo = CASE WHEN LOWER(LTRIM(RTRIM(t.Tipo))) = 'ingreso' THEN N'Ingreso' ELSE N'Gasto' END
   AND LOWER(c.Nombre) = LOWER(COALESCE(NULLIF(LEFT(LTRIM(RTRIM(t.Categoria)), 60), N''), N'General'));


-- Salvaguarda para categorías antiguas vacías o datos históricos atípicos.
IF EXISTS (SELECT 1 FROM Transacciones WHERE IdCategoria IS NULL AND LOWER(LTRIM(RTRIM(Tipo))) = 'ingreso')
AND NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = N'General' AND Tipo = N'Ingreso')
    INSERT INTO Categorias (Nombre, Tipo, Descripcion, Activa, FechaCreacion)
    VALUES (N'General', N'Ingreso', N'Categoría de respaldo para registros históricos.', 1, SYSUTCDATETIME());
IF EXISTS (SELECT 1 FROM Transacciones WHERE IdCategoria IS NULL AND LOWER(LTRIM(RTRIM(Tipo))) <> 'ingreso')
AND NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = N'General' AND Tipo = N'Gasto')
    INSERT INTO Categorias (Nombre, Tipo, Descripcion, Activa, FechaCreacion)
    VALUES (N'General', N'Gasto', N'Categoría de respaldo para registros históricos.', 1, SYSUTCDATETIME());

UPDATE t SET IdCategoria = c.IdCategoria
FROM Transacciones t
INNER JOIN Categorias c ON c.Nombre = N'General'
 AND c.Tipo = CASE WHEN LOWER(LTRIM(RTRIM(t.Tipo))) = 'ingreso' THEN N'Ingreso' ELSE N'Gasto' END
WHERE t.IdCategoria IS NULL;
");
        migrationBuilder.AlterColumn<int>(name: "IdCategoria", table: "Transacciones", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

        migrationBuilder.DropForeignKey(name: "FK_Transacciones_Clientes_ClienteId", table: "Transacciones");
        migrationBuilder.DropForeignKey(name: "FK_Transacciones_Usuarios_UsuarioId", table: "Transacciones");
        migrationBuilder.DropIndex(name: "IX_Transacciones_ClienteId", table: "Transacciones");
        migrationBuilder.DropIndex(name: "IX_Transacciones_UsuarioId", table: "Transacciones");
        migrationBuilder.DropColumn(name: "ClienteId", table: "Transacciones");
        migrationBuilder.DropColumn(name: "Tipo", table: "Transacciones");
        migrationBuilder.DropColumn(name: "Categoria", table: "Transacciones");
        migrationBuilder.RenameColumn(name: "UsuarioId", table: "Transacciones", newName: "IdUsuarioRegistro");

        migrationBuilder.CreateIndex(name: "IX_Categorias_Nombre_Tipo", table: "Categorias", columns: new[] { "Nombre", "Tipo" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Transacciones_FechaMovimiento", table: "Transacciones", column: "FechaMovimiento");
        migrationBuilder.CreateIndex(name: "IX_Transacciones_IdCategoria", table: "Transacciones", column: "IdCategoria");
        migrationBuilder.CreateIndex(name: "IX_Transacciones_IdUsuarioRegistro", table: "Transacciones", column: "IdUsuarioRegistro");
        migrationBuilder.CreateIndex(name: "IX_Transacciones_IdSesionCaja", table: "Transacciones", column: "IdSesionCaja");
        migrationBuilder.CreateIndex(name: "IX_SesionesCaja_IdUsuarioApertura", table: "SesionesCaja", column: "IdUsuarioApertura", unique: true, filter: "[Estado] = 'ABIERTA'");
        migrationBuilder.CreateIndex(name: "IX_SesionesCaja_IdUsuarioCierre", table: "SesionesCaja", column: "IdUsuarioCierre");
        migrationBuilder.CreateIndex(name: "IX_AuditoriaAcciones_FechaHora", table: "AuditoriaAcciones", column: "FechaHora");
        migrationBuilder.CreateIndex(name: "IX_AuditoriaAcciones_Entidad_IdRegistro", table: "AuditoriaAcciones", columns: new[] { "Entidad", "IdRegistro" });
        migrationBuilder.CreateIndex(name: "IX_AuditoriaAcciones_IdUsuario", table: "AuditoriaAcciones", column: "IdUsuario");

        migrationBuilder.Sql("UPDATE Transacciones SET Monto = ABS(Monto) WHERE Monto < 0; UPDATE Transacciones SET Estado = 'ANULADA' WHERE Monto = 0;");
        migrationBuilder.AddCheckConstraint(name: "CK_Transacciones_MontoPositivo", table: "Transacciones", sql: "[Monto] > 0 OR [Estado] = 'ANULADA'");
        migrationBuilder.AddCheckConstraint(name: "CK_Transacciones_Estado", table: "Transacciones", sql: "[Estado] IN ('REGISTRADA', 'ANULADA')");
        migrationBuilder.AddForeignKey(name: "FK_Transacciones_Categorias_IdCategoria", table: "Transacciones", column: "IdCategoria", principalTable: "Categorias", principalColumn: "IdCategoria", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_Transacciones_Usuarios_IdUsuarioRegistro", table: "Transacciones", column: "IdUsuarioRegistro", principalTable: "Usuarios", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_Transacciones_SesionesCaja_IdSesionCaja", table: "Transacciones", column: "IdSesionCaja", principalTable: "SesionesCaja", principalColumn: "IdSesionCaja", onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropTable(name: "Clientes");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Transacciones_Categorias_IdCategoria", table: "Transacciones");
        migrationBuilder.DropForeignKey(name: "FK_Transacciones_Usuarios_IdUsuarioRegistro", table: "Transacciones");
        migrationBuilder.DropForeignKey(name: "FK_Transacciones_SesionesCaja_IdSesionCaja", table: "Transacciones");
        migrationBuilder.DropIndex(name: "IX_Transacciones_FechaMovimiento", table: "Transacciones");
        migrationBuilder.DropCheckConstraint(name: "CK_Transacciones_MontoPositivo", table: "Transacciones");
        migrationBuilder.DropCheckConstraint(name: "CK_Transacciones_Estado", table: "Transacciones");
        migrationBuilder.DropIndex(name: "IX_Transacciones_IdCategoria", table: "Transacciones");
        migrationBuilder.DropIndex(name: "IX_Transacciones_IdUsuarioRegistro", table: "Transacciones");
        migrationBuilder.DropIndex(name: "IX_Transacciones_IdSesionCaja", table: "Transacciones");
        migrationBuilder.DropIndex(name: "IX_Categorias_Nombre_Tipo", table: "Categorias");

        migrationBuilder.AddColumn<string>(name: "Tipo", table: "Transacciones", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Ingreso");
        migrationBuilder.AddColumn<string>(name: "Categoria", table: "Transacciones", type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: "General");
        migrationBuilder.Sql(@"
UPDATE t SET Tipo = c.Tipo, Categoria = c.Nombre
FROM Transacciones t INNER JOIN Categorias c ON t.IdCategoria = c.IdCategoria;
");
        migrationBuilder.DropColumn(name: "IdCategoria", table: "Transacciones");
        migrationBuilder.DropColumn(name: "IdSesionCaja", table: "Transacciones");
        migrationBuilder.DropColumn(name: "Estado", table: "Transacciones");
        migrationBuilder.DropColumn(name: "FechaRegistro", table: "Transacciones");
        migrationBuilder.RenameColumn(name: "FechaMovimiento", table: "Transacciones", newName: "Fecha");
        migrationBuilder.RenameColumn(name: "IdUsuarioRegistro", table: "Transacciones", newName: "UsuarioId");

        migrationBuilder.DropTable(name: "AuditoriaAcciones");
        migrationBuilder.DropTable(name: "SesionesCaja");
        migrationBuilder.DropTable(name: "Categorias");

        migrationBuilder.DropForeignKey(name: "FK_Usuarios_Roles_IdRol", table: "Usuarios");
        migrationBuilder.DropIndex(name: "IX_Usuarios_IdRol", table: "Usuarios");
        migrationBuilder.AddColumn<string>(name: "Rol", table: "Usuarios", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Cajero");
        migrationBuilder.Sql(@"UPDATE u SET Rol = r.Nombre FROM Usuarios u INNER JOIN Roles r ON u.IdRol = r.IdRol;");
        migrationBuilder.DropColumn(name: "IdRol", table: "Usuarios");
        migrationBuilder.DropColumn(name: "Activo", table: "Usuarios");
        migrationBuilder.DropTable(name: "Roles");
        migrationBuilder.AlterColumn<string>(name: "PasswordHash", table: "Usuarios", type: "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(255)", oldMaxLength: 255);

        // Se recrea la estructura antigua de Clientes vacía: los datos eliminados no pueden recuperarse desde Down.
        migrationBuilder.CreateTable(name: "Clientes", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
            CedulaRuc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
            Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
            Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            SaldoPendiente = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
            Estado = table.Column<bool>(type: "bit", nullable: false),
            Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
            UsuarioId = table.Column<int>(type: "int", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Clientes", x => x.Id);
            table.ForeignKey("FK_Clientes_Usuarios_UsuarioId", x => x.UsuarioId, "Usuarios", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex(name: "IX_Clientes_UsuarioId", table: "Clientes", column: "UsuarioId");
        migrationBuilder.AddColumn<int>(name: "ClienteId", table: "Transacciones", type: "int", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Transacciones_ClienteId", table: "Transacciones", column: "ClienteId");
        migrationBuilder.CreateIndex(name: "IX_Transacciones_UsuarioId", table: "Transacciones", column: "UsuarioId");
        migrationBuilder.AddForeignKey(name: "FK_Transacciones_Usuarios_UsuarioId", table: "Transacciones", column: "UsuarioId", principalTable: "Usuarios", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_Transacciones_Clientes_ClienteId", table: "Transacciones", column: "ClienteId", principalTable: "Clientes", principalColumn: "Id");
    }
}
