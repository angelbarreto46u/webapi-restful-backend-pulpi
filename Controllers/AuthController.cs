using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using webapi_restful_backend_pulpi.Data;
using webapi_restful_backend_pulpi.DTOs;
using webapi_restful_backend_pulpi.Models;
using webapi_restful_backend_pulpi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace webapi_restful_backend_pulpi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly PulpiDbContext _context;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _environment;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string AuthCookieName = "pulpi_access_token";
    private static readonly TimeSpan TokenLifetime =
        TimeSpan.FromDays(7);

    public AuthController(
        PulpiDbContext context,
        IConfiguration config,
        IWebHostEnvironment environment,
    IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _config = config;
        _environment = environment;
        _scopeFactory = scopeFactory;
    }

    // ============================================================
    // INICIAR SESION
    // ============================================================

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginDto dto)
    {
        var username = dto.Username.Trim().ToLowerInvariant();

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u =>
                u.Username.ToLower() == username &&
                u.Activo);

        if (usuario is null ||
            !VerifyPasswordAndUpgrade(dto.Password, usuario))
        {
            return Unauthorized(new
            {
                message = "Usuario o contraseña incorrectos."
            });
        }

        await _context.SaveChangesAsync();

        var response = ToAuthResponse(usuario);

        // La cookie permite que Swagger utilice la sesión
        // sin pulsar Authorize ni introducir el token manualmente.
        SetAuthCookie(response.Token);

        // Se conserva el token en la respuesta por compatibilidad
        // con el frontend React actual.
        return Ok(response);
    }

    // ============================================================
    // REGISTRO
    // Los registros publicos siempre reciben el rol Cajero.
    // ============================================================

    [AllowAnonymous]
    [HttpPost("registro")]
    public async Task<ActionResult<AuthResponseDto>> Registro(
    [FromBody] RegistroDto dto)
    {
        if (!string.Equals(
            dto.Password,
            dto.ConfirmarPassword,
            StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "Las contraseñas no coinciden."
            });
        }

        if (Encoding.UTF8.GetByteCount(dto.Password) > 72)
        {
            return BadRequest(new
            {
                message =
                    "La contraseña no puede superar 72 bytes en UTF-8. " +
                    "Usa menos caracteres si contiene tildes o símbolos."
            });
        }

        var username = dto.Username.Trim().ToLowerInvariant();
        var email = dto.Email.Trim().ToLowerInvariant();

        // Verificar si el nombre de usuario ya existe.
        if (await _context.Usuarios.AnyAsync(
            u => u.Username.ToLower() == username))
        {
            return Conflict(new
            {
                message = "El nombre de usuario ya está registrado."
            });
        }

        // Verificar si el correo electrónico ya existe.
        if (await _context.Usuarios.AnyAsync(
            u => u.Email.ToLower() == email))
        {
            return Conflict(new
            {
                message = "El correo electrónico ya está registrado."
            });
        }

        // Obtener el rol predeterminado para los registros públicos.
        var cajero = await _context.Roles
            .SingleOrDefaultAsync(r => r.Nombre == "Cajero");

        if (cajero is null)
        {
            return StatusCode(500, new
            {
                message =
                    "No existe el rol Cajero. Verifica la configuración de la base de datos."
            });
        }

        // Generar el hash una sola vez, fuera de los posibles reintentos.
        var passwordHash =
            BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // Obtener la estrategia de ejecución configurada para SQL Server.
        var strategy = _context.Database.CreateExecutionStrategy();

        // Ejecutar toda la transacción dentro de la estrategia de reintentos.
        var response = await strategy.ExecuteAsync(async () =>
        {
            // Crear un ámbito nuevo para disponer de un DbContext independiente.
            await using var scope = _scopeFactory.CreateAsyncScope();

            var transactionContext =
                scope.ServiceProvider.GetRequiredService<PulpiDbContext>();

            await using var dbTransaction =
                await transactionContext.Database.BeginTransactionAsync();

            // Obtener el rol desde el contexto que ejecutará la transacción.
            var rolRegistro = await transactionContext.Roles
                .SingleAsync(r => r.IdRol == cajero.IdRol);

            var usuario = new Usuario
            {
                NombreCompleto = dto.NombreCompleto.Trim(),
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                IdRol = rolRegistro.IdRol,
                Rol = rolRegistro,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            };

            // Guardar el usuario.
            transactionContext.Usuarios.Add(usuario);

            await transactionContext.SaveChangesAsync();

            // Registrar la operación en la auditoría.
            AuditoriaHelper.Registrar(
                transactionContext,
                usuario.Id,
                "Usuarios",
                usuario.Id,
                "REGISTRO",
                despues: new
                {
                    usuario.Username,
                    usuario.Email,
                    Rol = rolRegistro.Nombre
                });

            // Guardar el registro de auditoría.
            await transactionContext.SaveChangesAsync();

            // Generar la respuesta antes de confirmar la transacción.
            var authResponse = ToAuthResponse(usuario);

            // Confirmar la transacción.
            await dbTransaction.CommitAsync();

            return authResponse;
        });

        // Establecer la cookie después de completar la transacción.
        SetAuthCookie(response.Token);

        return Ok(response);
    }

    // ============================================================
    // CERRAR SESION
    // ============================================================

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(
            AuthCookieName,
            new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = CookieSecure,
                SameSite = SameSiteMode.Lax
            });

        return NoContent();
    }

    // ============================================================
    // OBTENER USUARIO ACTUAL Y SINCRONIZAR COOKIE
    // ============================================================

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioDto>> Me()
    {
        var usuario = await GetCurrentUserAsync();

        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new
            {
                message = "La cuenta no existe o está desactivada."
            });
        }

        // Si React ya tiene un JWT válido en Authorization,
        // se establece también la cookie para Swagger.
        // Si la solicitud llegó mediante cookie, se renueva
        // usando el mismo token validado.
        var token = GetIncomingJwt();

        if (!string.IsNullOrWhiteSpace(token))
        {
            SetAuthCookie(token);
        }

        return Ok(ToUsuarioDto(usuario));
    }

    // ============================================================
    // ACTUALIZAR PERFIL
    // ============================================================

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<AuthResponseDto>> UpdateProfile(
        [FromBody] ActualizarPerfilDto dto)
    {
        var usuario = await GetCurrentUserAsync();

        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized();
        }

        var email = dto.Email.Trim().ToLowerInvariant();

        if (await _context.Usuarios.AnyAsync(
            u => u.Id != usuario.Id &&
                 u.Email.ToLower() == email))
        {
            return Conflict(new
            {
                message =
                    "El correo electrónico ya está asociado a otra cuenta."
            });
        }

        var before = new
        {
            usuario.NombreCompleto,
            usuario.Email
        };

        usuario.NombreCompleto = dto.NombreCompleto.Trim();
        usuario.Email = email;

        AuditoriaHelper.Registrar(
            _context,
            usuario.Id,
            "Usuarios",
            usuario.Id,
            "ACTUALIZAR_PERFIL",
            before,
            new
            {
                usuario.NombreCompleto,
                usuario.Email
            });

        await _context.SaveChangesAsync();

        // Se emite un JWT actualizado y se sincroniza la cookie.
        var response = ToAuthResponse(usuario);
        SetAuthCookie(response.Token);

        return Ok(response);
    }

    // ============================================================
    // CAMBIAR CONTRASEÑA
    // ============================================================

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] CambiarPasswordDto dto)
    {
        var usuario = await GetCurrentUserAsync();

        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized();
        }

        if (!VerifyPasswordAndUpgrade(
            dto.PasswordActual,
            usuario))
        {
            return BadRequest(new
            {
                message = "La contraseña actual es incorrecta."
            });
        }

        if (!string.Equals(
            dto.Password,
            dto.ConfirmarPassword,
            StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "Las contraseñas no coinciden."
            });
        }

        if (Encoding.UTF8.GetByteCount(dto.Password) > 72)
        {
            return BadRequest(new
            {
                message =
                    "La contraseña no puede superar 72 bytes en UTF-8."
            });
        }

        usuario.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(dto.Password);

        AuditoriaHelper.Registrar(
            _context,
            usuario.Id,
            "Usuarios",
            usuario.Id,
            "CAMBIAR_PASSWORD");

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // ============================================================
    // LISTAR USUARIOS - SOLO ADMINISTRADOR
    // ============================================================

    [Authorize(Roles = "Administrador")]
    [HttpGet("usuarios")]
    public async Task<ActionResult<IEnumerable<UsuarioDto>>> GetUsers()
    {
        var usuarios = await _context.Usuarios
            .Include(u => u.Rol)
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new UsuarioDto(
                u.Id,
                u.Username,
                u.Email,
                u.NombreCompleto,
                u.Rol!.Nombre,
                u.FechaRegistro,
                u.Activo))
            .ToListAsync();

        return Ok(usuarios);
    }

    // ============================================================
    // CAMBIAR ROL - SOLO ADMINISTRADOR
    // ============================================================

    [Authorize(Roles = "Administrador")]
    [HttpPut("usuarios/{id:int}/rol")]
    public async Task<IActionResult> UpdateRole(
        int id,
        [FromBody] CambiarRolDto dto)
    {
        var currentId = GetCurrentUserId();

        if (currentId is null)
        {
            return Unauthorized();
        }

        if (id == currentId.Value)
        {
            return BadRequest(new
            {
                message = "No puedes cambiar el rol de tu propia cuenta."
            });
        }

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario is null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado."
            });
        }

        var targetRole = await _context.Roles
            .SingleOrDefaultAsync(r => r.Nombre == dto.Rol);

        if (targetRole is null)
        {
            return BadRequest(new
            {
                message = "El rol solicitado no existe."
            });
        }

        if (usuario.Rol?.Nombre == "Administrador" &&
            targetRole.Nombre != "Administrador")
        {
            var adminCount = await _context.Usuarios
                .CountAsync(u =>
                    u.Activo &&
                    u.Rol!.Nombre == "Administrador");

            if (adminCount <= 1)
            {
                return Conflict(new
                {
                    message =
                        "No se puede quitar el rol al último administrador activo."
                });
            }
        }

        var before = new
        {
            usuario.Id,
            usuario.Username,
            Rol = usuario.Rol!.Nombre
        };

        usuario.IdRol = targetRole.IdRol;
        usuario.Rol = targetRole;

        AuditoriaHelper.Registrar(
            _context,
            currentId.Value,
            "Usuarios",
            usuario.Id,
            "CAMBIAR_ROL",
            before,
            new
            {
                usuario.Id,
                usuario.Username,
                Rol = targetRole.Nombre
            });

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // ============================================================
    // ACTIVAR / DESACTIVAR USUARIO - SOLO ADMINISTRADOR
    // ============================================================

    [Authorize(Roles = "Administrador")]
    [HttpPut("usuarios/{id:int}/estado")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] CambiarEstadoUsuarioDto dto)
    {
        var currentId = GetCurrentUserId();

        if (currentId is null)
        {
            return Unauthorized();
        }

        if (!dto.Activo.HasValue)
        {
            return BadRequest(new
            {
                message = "El estado de la cuenta es obligatorio."
            });
        }

        var active = dto.Activo.Value;

        if (id == currentId.Value && !active)
        {
            return BadRequest(new
            {
                message = "No puedes desactivar tu propia cuenta."
            });
        }

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario is null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado."
            });
        }

        if (!active &&
            usuario.Rol?.Nombre == "Administrador" &&
            await _context.Usuarios.CountAsync(u =>
                u.Activo &&
                u.Rol!.Nombre == "Administrador") <= 1)
        {
            return Conflict(new
            {
                message =
                    "No se puede desactivar al último administrador activo."
            });
        }

        var before = new
        {
            usuario.Activo
        };

        usuario.Activo = active;

        AuditoriaHelper.Registrar(
            _context,
            currentId.Value,
            "Usuarios",
            usuario.Id,
            active ? "ACTIVAR_USUARIO" : "DESACTIVAR_USUARIO",
            before,
            new
            {
                usuario.Activo
            });

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // ============================================================
    // METODOS PRIVADOS
    // ============================================================

    private async Task<Usuario?> GetCurrentUserAsync()
    {
        var id = GetCurrentUserId();

        return id.HasValue
            ? await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == id.Value)
            : null;
    }

    private int? GetCurrentUserId()
    {
        var rawId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return int.TryParse(rawId, out var id)
            ? id
            : null;
    }

    private AuthResponseDto ToAuthResponse(Usuario usuario)
    {
        return new AuthResponseDto(
            usuario.Id,
            GenerarToken(usuario),
            usuario.Username,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Rol?.Nombre ?? "Cajero",
            usuario.FechaRegistro);
    }

    private static UsuarioDto ToUsuarioDto(Usuario usuario)
    {
        return new UsuarioDto(
            usuario.Id,
            usuario.Username,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Rol?.Nombre ?? "Cajero",
            usuario.FechaRegistro,
            usuario.Activo);
    }

    private bool CookieSecure =>
        !_environment.IsDevelopment() || Request.IsHttps;

    private string? GetIncomingJwt()
    {
        var authorization =
            Request.Headers.Authorization.ToString();

        if (authorization.StartsWith(
            "Bearer ",
            StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return Request.Cookies.TryGetValue(
            AuthCookieName,
            out var cookieToken)
                ? cookieToken
                : null;
    }

    private void SetAuthCookie(string token)
    {
        Response.Cookies.Append(
            AuthCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.Add(TokenLifetime)
            });
    }

    private string GenerarToken(Usuario usuario)
    {
        var keyText = _config["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Key.");

        var issuer = _config["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Issuer.");

        var audience = _config["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Audience.");

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(keyText));

        var creds = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                usuario.Id.ToString()),

            new Claim(
                ClaimTypes.Name,
                usuario.Username),

            new Claim(
                ClaimTypes.Role,
                usuario.Rol?.Nombre ?? "Cajero"),

            new Claim(
                ClaimTypes.Email,
                usuario.Email)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(TokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private static bool VerifyPasswordAndUpgrade(
        string password,
        Usuario usuario,
        bool saveUpgrade = true)
    {
        // Hashes BCrypt actuales.
        if (usuario.PasswordHash.StartsWith(
            "$2",
            StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(
                    password,
                    usuario.PasswordHash);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        // Compatibilidad con hashes SHA-256 antiguos.
        if (usuario.PasswordHash.Length != 64)
        {
            return false;
        }

        var oldHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(password)))
            .ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(oldHash),
            Encoding.UTF8.GetBytes(
                usuario.PasswordHash.ToLowerInvariant())))
        {
            return false;
        }

        if (saveUpgrade)
        {
            usuario.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(password);
        }

        return true;
    }
}