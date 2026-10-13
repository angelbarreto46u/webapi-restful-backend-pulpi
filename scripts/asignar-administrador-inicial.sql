/*
  Pulpi — promoción segura del primer administrador
  Ejecute este script en la base de datos configurada en appsettings.json.
  Reemplace el marcador por el Username exacto de una cuenta ya registrada y activa.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Username NVARCHAR(50) = N'REEMPLAZA_POR_TU_USUARIO';

IF @Username = N'REEMPLAZA_POR_TU_USUARIO'
    THROW 51000, N'Reemplace @Username por el usuario registrado antes de ejecutar el script.', 1;

DECLARE @UserId INT;
SELECT @UserId = u.Id
FROM dbo.Usuarios AS u
WHERE u.Username = @Username AND u.Activo = 1;

IF @UserId IS NULL
    THROW 51001, N'No se encontró una cuenta activa con ese Username. Registre la cuenta e inténtelo de nuevo.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.Usuarios AS u
    INNER JOIN dbo.Roles AS r ON r.IdRol = u.IdRol
    WHERE u.Activo = 1 AND r.Nombre = N'Administrador'
)
    THROW 51002, N'Ya existe un administrador activo. Use la pantalla Usuario y roles para administrar los demás usuarios.', 1;

DECLARE @AdminRoleId INT;
SELECT @AdminRoleId = IdRol FROM dbo.Roles WHERE Nombre = N'Administrador';
IF @AdminRoleId IS NULL
    THROW 51003, N'No se encontró el rol Administrador. Compruebe que las migraciones se aplicaron correctamente.', 1;

BEGIN TRANSACTION;
UPDATE dbo.Usuarios SET IdRol = @AdminRoleId WHERE Id = @UserId AND Activo = 1;
IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51004, N'No se pudo promover la cuenta seleccionada.', 1;
END;
COMMIT TRANSACTION;

SELECT u.Id, u.Username, u.Email, r.Nombre AS Rol, u.Activo
FROM dbo.Usuarios AS u
INNER JOIN dbo.Roles AS r ON r.IdRol = u.IdRol
WHERE u.Id = @UserId;
