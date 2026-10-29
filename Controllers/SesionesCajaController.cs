
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using webapi_restful_backend_pulpi.Data;
using webapi_restful_backend_pulpi.DTOs;
using webapi_restful_backend_pulpi.Models;
using webapi_restful_backend_pulpi.Services;

namespace webapi_restful_backend_pulpi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SesionesCajaController : ControllerBase
{
    private readonly PulpiDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;

    public SesionesCajaController(
        PulpiDbContext context,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _scopeFactory = scopeFactory;
    }

    // ============================================================
    // LISTAR SESIONES DE CAJA
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        IQueryable<SesionCaja> query = _context.SesionesCaja
            .AsNoTracking()
            .Include(s => s.UsuarioApertura)
            .Include(s => s.UsuarioCierre);

        if (!User.IsInRole("Administrador"))
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            query = query.Where(
                s => s.IdUsuarioApertura == userId);
        }

        var sessions = await query
            .OrderByDescending(s => s.FechaHoraApertura)
            .Take(200)
            .ToListAsync();

        return Ok(sessions.Select(s => ToResponse(s)));
    }

    // ============================================================
    // OBTENER SESIÓN ACTUAL
    // ============================================================

    [HttpGet("actual")]
    public async Task<IActionResult> GetCurrent()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var session = await _context.SesionesCaja
            .AsNoTracking()
            .Include(s => s.UsuarioApertura)
            .Include(s => s.UsuarioCierre)
            .FirstOrDefaultAsync(s =>
                s.IdUsuarioApertura == userId &&
                s.Estado == "ABIERTA");

        return Ok(
            session is null
                ? null
                : ToResponse(session));
    }

    // ============================================================
    // ABRIR SESIÓN DE CAJA
    // ============================================================

    [HttpPost("abrir")]
    public async Task<IActionResult> Open(
        [FromBody] AbrirSesionCajaDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!dto.SaldoInicial.HasValue)
        {
            return BadRequest(new
            {
                message = "El saldo inicial es obligatorio."
            });
        }

        // Obtener la estrategia configurada para SQL Server.
        var strategy =
            _context.Database.CreateExecutionStrategy();

        // Toda la operación transaccional se ejecuta dentro
        // de la estrategia de reintentos.
        return await strategy.ExecuteAsync<IActionResult>(
            async () =>
            {
                // Contexto nuevo para este intento.
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PulpiDbContext>();

                await using var transaction =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                // Evitar que el usuario tenga dos sesiones abiertas.
                var current = await db.SesionesCaja.AnyAsync(s =>
                    s.IdUsuarioApertura == userId &&
                    s.Estado == "ABIERTA");

                if (current)
                {
                    return new ConflictObjectResult(new
                    {
                        message =
                            "Ya tienes una sesión de caja abierta. " +
                            "Ciérrala antes de abrir otra."
                    });
                }

                var session = new SesionCaja
                {
                    IdUsuarioApertura = userId,
                    FechaHoraApertura = DateTime.UtcNow,
                    SaldoInicial = dto.SaldoInicial.Value,
                    Estado = "ABIERTA",
                    Observaciones =
                        string.IsNullOrWhiteSpace(dto.Observaciones)
                            ? null
                            : dto.Observaciones.Trim()
                };

                db.SesionesCaja.Add(session);

                await db.SaveChangesAsync();

                // Registrar auditoría dentro de la misma transacción.
                AuditoriaHelper.Registrar(
                    db,
                    userId,
                    "SesionesCaja",
                    session.IdSesionCaja,
                    "ABRIR",
                    despues: new
                    {
                        session.IdSesionCaja,
                        session.SaldoInicial,
                        session.FechaHoraApertura,
                        session.Estado,
                        session.Observaciones
                    });

                await db.SaveChangesAsync();

                // Obtener el nombre del usuario antes de confirmar.
                await db.Entry(session)
                    .Reference(s => s.UsuarioApertura)
                    .LoadAsync();

                var response = ToResponse(session);

                await transaction.CommitAsync();

                return new OkObjectResult(response);
            });
    }

    // ============================================================
    // CERRAR SESIÓN DE CAJA
    // ============================================================

    [HttpPost("{id:long}/cerrar")]
    public async Task<IActionResult> Close(
        long id,
        [FromBody] CerrarSesionCajaDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var isAdmin = User.IsInRole("Administrador");

        var strategy =
            _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(
            async () =>
            {
                // Contexto nuevo para este intento.
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PulpiDbContext>();

                await using var transaction =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                // Buscar una sesión abierta y comprobar permisos.
                IQueryable<SesionCaja> sessionQuery =
                    db.SesionesCaja
                        .Include(s => s.UsuarioApertura)
                        .Include(s => s.UsuarioCierre)
                        .Where(s =>
                            s.IdSesionCaja == id &&
                            s.Estado == "ABIERTA");

                if (!isAdmin)
                {
                    sessionQuery = sessionQuery.Where(
                        s => s.IdUsuarioApertura == userId);
                }

                var session =
                    await sessionQuery.FirstOrDefaultAsync();

                if (session is null)
                {
                    return new NotFoundObjectResult(new
                    {
                        message =
                            "No se encontró una sesión abierta " +
                            "accesible para cerrar."
                    });
                }

                // Obtener movimientos en efectivo registrados.
                var movements = db.Transacciones
                    .Include(t => t.Categoria)
                    .Where(t =>
                        t.IdSesionCaja == id &&
                        t.Estado == "REGISTRADA" &&
                        t.MetodoPago == "Efectivo");

                var incomes = await movements
                    .Where(t => t.Categoria!.Tipo == "Ingreso")
                    .SumAsync(t => (decimal?)t.Monto) ?? 0m;

                var expenses = await movements
                    .Where(t => t.Categoria!.Tipo == "Gasto")
                    .SumAsync(t => (decimal?)t.Monto) ?? 0m;

                // El saldo definitivo se calcula en el servidor.
                var expected =
                    session.SaldoInicial + incomes - expenses;

                var before = ToResponse(session);

                session.IdUsuarioCierre = userId;
                session.FechaHoraCierre = DateTime.UtcNow;
                session.SaldoEsperado = expected;
                session.SaldoDeclarado = expected;
                session.Estado = "CERRADA";

                if (!string.IsNullOrWhiteSpace(dto.Observaciones))
                {
                    session.Observaciones =
                        string.IsNullOrWhiteSpace(session.Observaciones)
                            ? dto.Observaciones.Trim()
                            : $"{session.Observaciones} | Cierre: " +
                              dto.Observaciones.Trim();
                }

                AuditoriaHelper.Registrar(
                    db,
                    userId,
                    "SesionesCaja",
                    session.IdSesionCaja,
                    "CERRAR",
                    before,
                    new
                    {
                        session.FechaHoraCierre,
                        session.SaldoEsperado,
                        session.SaldoDeclarado,
                        session.Estado,
                        session.Observaciones
                    });

                await db.SaveChangesAsync();

                // Consultar el nombre antes de confirmar la transacción.
                var closingUserName = await db.Usuarios
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.NombreCompleto)
                    .FirstOrDefaultAsync();

                var response = ToResponse(
                    session,
                    usuarioCierreNombre: closingUserName);

                await transaction.CommitAsync();

                return new OkObjectResult(response);
            });
    }

    // ============================================================
    // UTILIDADES
    // ============================================================

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }

    private static object ToResponse(
        SesionCaja s,
        string? usuarioCierreNombre = null)
    {
        return new
        {
            s.IdSesionCaja,
            s.IdUsuarioApertura,
            usuarioApertura = s.UsuarioApertura?.NombreCompleto,
            s.FechaHoraApertura,
            s.SaldoInicial,
            s.IdUsuarioCierre,
            usuarioCierre =
                usuarioCierreNombre ?? s.UsuarioCierre?.NombreCompleto,
            s.FechaHoraCierre,
            s.SaldoEsperado,
            s.SaldoDeclarado,
            s.Estado,
            s.Observaciones
        };
    }
}
