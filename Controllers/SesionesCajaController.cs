using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public SesionesCajaController(PulpiDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        IQueryable<SesionCaja> query = _context.SesionesCaja.AsNoTracking().Include(s => s.UsuarioApertura).Include(s => s.UsuarioCierre);
        if (!User.IsInRole("Administrador"))
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            query = query.Where(s => s.IdUsuarioApertura == userId);
        }
        var sessions = await query.OrderByDescending(s => s.FechaHoraApertura).Take(200).ToListAsync();
        return Ok(sessions.Select(ToResponse));
    }

    [HttpGet("actual")]
    public async Task<IActionResult> GetCurrent()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var session = await _context.SesionesCaja.AsNoTracking()
            .Include(s => s.UsuarioApertura).Include(s => s.UsuarioCierre)
            .FirstOrDefaultAsync(s => s.IdUsuarioApertura == userId && s.Estado == "ABIERTA");
        return Ok(session is null ? null : ToResponse(session));
    }

    [HttpPost("abrir")]
    public async Task<IActionResult> Open([FromBody] AbrirSesionCajaDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var current = await _context.SesionesCaja.AnyAsync(s => s.IdUsuarioApertura == userId && s.Estado == "ABIERTA");
        if (current) return Conflict(new { message = "Ya tienes una sesión de caja abierta. Ciérrala antes de abrir otra." });

        var session = new SesionCaja
        {
            IdUsuarioApertura = userId,
            FechaHoraApertura = DateTime.UtcNow,
            SaldoInicial = dto.SaldoInicial!.Value,
            Estado = "ABIERTA",
            Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim()
        };
        _context.SesionesCaja.Add(session);
        await _context.SaveChangesAsync();
        AuditoriaHelper.Registrar(_context, userId, "SesionesCaja", session.IdSesionCaja, "ABRIR",
            despues: new { session.IdSesionCaja, session.SaldoInicial, session.FechaHoraApertura, session.Estado, session.Observaciones });
        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        await _context.Entry(session).Reference(s => s.UsuarioApertura).LoadAsync();
        return Ok(ToResponse(session));
    }

    [HttpPost("{id:long}/cerrar")]
    public async Task<IActionResult> Close(long id, [FromBody] CerrarSesionCajaDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        IQueryable<SesionCaja> sessionQuery = _context.SesionesCaja.Include(s => s.UsuarioApertura).Include(s => s.UsuarioCierre)
            .Where(s => s.IdSesionCaja == id && s.Estado == "ABIERTA");
        if (!User.IsInRole("Administrador")) sessionQuery = sessionQuery.Where(s => s.IdUsuarioApertura == userId);
        var session = await sessionQuery.FirstOrDefaultAsync();
        if (session is null) return NotFound(new { message = "No se encontró una sesión abierta accesible para cerrar." });

        var movements = _context.Transacciones.Include(t => t.Categoria)
            .Where(t => t.IdSesionCaja == id && t.Estado == "REGISTRADA" && t.MetodoPago == "Efectivo");
        var incomes = await movements.Where(t => t.Categoria!.Tipo == "Ingreso").SumAsync(t => (decimal?)t.Monto) ?? 0m;
        var expenses = await movements.Where(t => t.Categoria!.Tipo == "Gasto").SumAsync(t => (decimal?)t.Monto) ?? 0m;
        var expected = session.SaldoInicial + incomes - expenses;
        var before = ToResponse(session);

        session.IdUsuarioCierre = userId;
        session.FechaHoraCierre = DateTime.UtcNow;

        // El saldo definitivo se calcula en el servidor.
        session.SaldoEsperado = expected;
        session.SaldoDeclarado = expected;
        session.Estado = "CERRADA";

        if (!string.IsNullOrWhiteSpace(dto.Observaciones))
        {
            session.Observaciones = string.IsNullOrWhiteSpace(session.Observaciones)
                ? dto.Observaciones.Trim()
                : $"{session.Observaciones} | Cierre: {dto.Observaciones.Trim()}";
        }

        AuditoriaHelper.Registrar(
            _context,
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

        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        session.UsuarioCierre = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        return Ok(ToResponse(session));
    }

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static object ToResponse(SesionCaja s) => new
    {
        s.IdSesionCaja,
        s.IdUsuarioApertura,
        usuarioApertura = s.UsuarioApertura?.NombreCompleto,
        s.FechaHoraApertura,
        s.SaldoInicial,
        s.IdUsuarioCierre,
        usuarioCierre = s.UsuarioCierre?.NombreCompleto,
        s.FechaHoraCierre,
        s.SaldoEsperado,
        s.SaldoDeclarado,
        s.Estado,
        s.Observaciones
    };
}
