# Pulpi API — ASP.NET Core 10 + SQL Server

API REST del sistema Pulpi: autenticación JWT, usuarios y roles, categorías, transacciones, sesiones de caja, balances, reportes y auditoría.

## Requisitos

- .NET SDK 10.
- SQL Server accesible desde la máquina donde se ejecutará la API.
- Acceso de escritura a la base de datos configurada.

## Configuración local

1. Ajusta `ConnectionStrings:DefaultConnection` en `appsettings.json` para apuntar a tu instancia de SQL Server. No publiques cadenas con credenciales.
2. Para desarrollo local, `appsettings.Development.json` contiene una clave de prueba. Antes de cualquier despliegue, define `Jwt__Key` como secreto aleatorio de al menos 32 bytes, habilita HTTPS y configura CORS con el origen real del frontend.
3. Desde esta carpeta ejecuta:

```powershell
dotnet restore
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet ef database update
dotnet run
```

Si ya está instalada `dotnet-ef`, omite el comando de instalación. Swagger se encuentra en `http://localhost:5266/swagger` durante desarrollo.

## Advertencia importante sobre la migración

La migración `20261013100000_AddFinancialCoreAndRemoveClients` **elimina la tabla `Clientes` y sus datos**, porque el módulo fue retirado del alcance solicitado. Antes de `dotnet ef database update`, haz un respaldo completo de la base de datos y exporta la tabla si necesitas conservar sus datos por motivos históricos. `Down` recrea solo la estructura de `Clientes`, no recupera los registros eliminados.

La migración normaliza los roles en la tabla `Roles`, crea el catálogo de `Categorias`, añade `SesionesCaja` y `AuditoriaAcciones`, y vincula cada transacción con su categoría. Los movimientos existentes se conservan y se asocian con las categorías migradas; se dejan sin sesión de caja, pues no es posible reconstruir de forma fiable una apertura/cierre histórica que no se había registrado.

## Crear el primer administrador

Por seguridad, los registros públicos nuevos siempre reciben el rol `Cajero`; no existe una contraseña administrativa común ni una cuenta demo que se promueva automáticamente. Después de aplicar las migraciones:

1. Arranca la API y el frontend, y registra una cuenta normal.
2. Abre `scripts/asignar-administrador-inicial.sql` en SQL Server Management Studio, reemplaza el marcador por el `Username` registrado y ejecuta el script sobre la base configurada.
3. Cierra sesión en Pulpi e inicia sesión de nuevo para actualizar el JWT y el rol.
4. Desde **Usuario y roles**, el administrador puede promover otros usuarios autorizados.

El script solo promueve una cuenta activa si todavía no existe un administrador activo. Protege el sistema para no crear otra cuenta administrativa si el rol ya está inicializado.

## Seguridad y permisos

- `Administrador`: acceso global a usuarios, categorías, transacciones registradas, sesiones de caja, balances, reportes y auditoría.
- `Cajero`: solo sus propias transacciones y sesiones; puede registrar movimientos con una sesión abierta. No tiene acceso a balances, reportes, auditoría ni a información de otros usuarios.
- El backend verifica JWT y consulta el rol/estado actual del usuario desde la base de datos. Ocultar una opción de menú no sustituye esta autorización del servidor.
- El registro valida formato de nombre, username, correo y contraseña, confirma la contraseña y asigna el rol `Cajero`. La validación de sintaxis de un email no demuestra que el buzón exista o pertenezca a esa persona; eso requeriría un flujo separado de verificación por correo.
- Las categorías con movimientos no se borran físicamente: se desactivan para preservar el historial.
- Las transacciones se anulan lógicamente y se auditan, en vez de borrarse físicamente.

## Modelo de datos

`Roles`, `Usuarios`, `Categorias`, `Transacciones`, `SesionesCaja` y `AuditoriaAcciones`. Los identificadores son generados por SQL Server; no se aceptan como campos editables en los formularios de creación del frontend.
