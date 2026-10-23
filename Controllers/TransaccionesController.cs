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
public sealed class TransaccionesController : ControllerBase
{
    private readonly PulpiDbContext _context;
    public TransaccionesController(PulpiDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = _context.Transacciones.Where(t => t.Estado == "REGISTRADA").AsNoTracking();
        if (!User.IsInRole("Administrador"))
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            query = query.Where(t => t.IdUsuarioRegistro == userId);
        }
        var items = await query.Include(t => t.Categoria).Include(t => t.UsuarioRegistro)
            .OrderByDescending(t => t.FechaMovimiento).ThenByDescending(t => t.Id)
            .ToListAsync();
        return Ok(items.Select(ToResponse));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var query = _context.Transacciones.Include(t => t.Categoria).Include(t => t.UsuarioRegistro)
            .Include(t => t.SesionCaja).Where(t => t.Estado == "REGISTRADA");
        if (!User.IsInRole("Administrador"))
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            query = query.Where(t => t.IdUsuarioRegistro == userId);
        }
        var transaction = await query.FirstOrDefaultAsync(t => t.Id == id);
        return transaction is null ? NotFound(new { message = "Transacción no encontrada." }) : Ok(ToResponse(transaction));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearTransaccionDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var category = await _context.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == dto.CategoriaId && c.Activa);
        if (category is null) return BadRequest(new { message = "La categoría seleccionada no existe o está desactivada." });
        if (!IsTodayInNicaragua(dto.FechaMovimiento))
        {
            return BadRequest(new
            {
                message = "La fecha de una nueva transacción debe ser la fecha actual de Nicaragua."
            });
        }

        var session = await _context.SesionesCaja.FirstOrDefaultAsync(s => s.IdUsuarioApertura == userId && s.Estado == "ABIERTA");
        if (session is null) return Conflict(new { message = "Debes abrir una sesión de caja antes de registrar movimientos." });

        var transaction = new Transaccion
        {
            Concepto = dto.Concepto.Trim(),
            IdCategoria = category.IdCategoria,
            Categoria = category,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            FechaMovimiento = GetNicaraguaToday(),
            FechaRegistro = DateTime.UtcNow,
            IdUsuarioRegistro = userId,
            IdSesionCaja = session.IdSesionCaja,
            Estado = "REGISTRADA"
        };

        _context.Transacciones.Add(transaction);
        await _context.SaveChangesAsync();
        await _context.Entry(transaction).Reference(t => t.UsuarioRegistro).LoadAsync();
        AuditoriaHelper.Registrar(_context, userId, "Transacciones", transaction.Id, "CREAR", despues: ToResponse(transaction));
        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, ToResponse(transaction));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] CrearTransaccionDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        IQueryable<Transaccion> query = _context.Transacciones.Include(t => t.Categoria).Include(t => t.UsuarioRegistro).Include(t => t.SesionCaja)
            .Where(t => t.Estado == "REGISTRADA");
        if (!User.IsInRole("Administrador")) query = query.Where(t => t.IdUsuarioRegistro == userId);
        var transaction = await query.FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null) return NotFound(new { message = "Transacción no encontrada." });
        if (!User.IsInRole("Administrador") && transaction.IdSesionCaja != null && transaction.SesionCaja?.Estado != "ABIERTA")
            return Conflict(new { message = "No puedes modificar un movimiento de una sesión de caja cerrada. Solicita una corrección al administrador." });

        var category = await _context.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == dto.CategoriaId && c.Activa);
        if (category is null) return BadRequest(new { message = "La categoría seleccionada no existe o está desactivada." });
        if (dto.FechaMovimiento.Date != transaction.FechaMovimiento.Date)
        {
            return BadRequest(new
            {
                message = "La fecha de una transacción registrada no se puede modificar."
            });
        }

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var before = ToResponse(transaction);
        var affectedSessionId = transaction.IdSesionCaja;
        transaction.Concepto = dto.Concepto.Trim();
        transaction.IdCategoria = category.IdCategoria;
        transaction.Categoria = category;
        transaction.Monto = dto.Monto;
        transaction.MetodoPago = dto.MetodoPago;
        AuditoriaHelper.Registrar(_context, userId, "Transacciones", transaction.Id, "ACTUALIZAR", before, ToResponse(transaction));
        await _context.SaveChangesAsync();
        await RecalculateClosedSessionAsync(affectedSessionId, userId);
        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        return Ok(ToResponse(transaction));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, [FromQuery] string? motivo = null)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        IQueryable<Transaccion> query = _context.Transacciones.Include(t => t.Categoria).Include(t => t.UsuarioRegistro).Include(t => t.SesionCaja)
            .Where(t => t.Estado == "REGISTRADA");
        if (!User.IsInRole("Administrador")) query = query.Where(t => t.IdUsuarioRegistro == userId);
        var transaction = await query.FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null) return NotFound(new { message = "Transacción no encontrada." });
        if (!User.IsInRole("Administrador") && transaction.IdSesionCaja != null && transaction.SesionCaja?.Estado != "ABIERTA")
            return Conflict(new { message = "No puedes anular un movimiento de una sesión cerrada. Solicita la corrección al administrador." });

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var before = ToResponse(transaction);
        var affectedSessionId = transaction.IdSesionCaja;
        transaction.Estado = "ANULADA";
        AuditoriaHelper.Registrar(_context, userId, "Transacciones", transaction.Id, "ANULAR", before,
            new { transaction.Estado }, string.IsNullOrWhiteSpace(motivo) ? "Anulación solicitada desde el sistema" : motivo);
        await _context.SaveChangesAsync();
        await RecalculateClosedSessionAsync(affectedSessionId, userId);
        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        return NoContent();
    }

    private async Task RecalculateClosedSessionAsync(
    long? sessionId,
    int userId)
    {
        if (!sessionId.HasValue)
            return;

        var session = await _context.SesionesCaja
            .FirstOrDefaultAsync(s =>
                s.IdSesionCaja == sessionId.Value &&
                s.Estado == "CERRADA");

        if (session is null)
            return;

        var before = new
        {
            session.SaldoEsperado
        };

        var cashMovements = _context.Transacciones
            .Include(t => t.Categoria)
            .Where(t =>
                t.IdSesionCaja == session.IdSesionCaja &&
                t.Estado == "REGISTRADA" &&
                t.MetodoPago == "Efectivo");

        var incomes = await cashMovements
            .Where(t => t.Categoria!.Tipo == "Ingreso")
            .SumAsync(t => (decimal?)t.Monto) ?? 0m;

        var expenses = await cashMovements
            .Where(t => t.Categoria!.Tipo == "Gasto")
            .SumAsync(t => (decimal?)t.Monto) ?? 0m;

        session.SaldoEsperado =
            session.SaldoInicial + incomes - expenses;

        AuditoriaHelper.Registrar(
            _context,
            userId,
            "SesionesCaja",
            session.IdSesionCaja,
            "RECALCULAR_SALDO_ESPERADO",
            before,
            new
            {
                session.SaldoEsperado
            },
            "El administrador corrigió o anuló un movimiento de una sesión ya cerrada.");
    }

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    private static bool IsValidDate(DateTime date) => date != default && date <= DateTime.UtcNow.AddDays(1);
    private static DateTime GetNicaraguaToday()
    {
        var timeZoneId = OperatingSystem.IsWindows()
            ? "Central America Standard Time"
            : "America/Managua";

        var nicaraguaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo
            .ConvertTimeFromUtc(DateTime.UtcNow, nicaraguaTimeZone)
            .Date;
    }

    private static bool IsTodayInNicaragua(DateTime date)
    {
        return date.Date == GetNicaraguaToday();
    }

    private static object ToResponse(Transaccion t) => new
    {
        idTransaccion = t.Id,
        concepto = t.Concepto,
        categoriaId = t.IdCategoria,
        categoria = t.Categoria?.Nombre ?? string.Empty,
        tipo = t.Categoria?.Tipo ?? string.Empty,
        monto = t.Monto,
        metodoPago = t.MetodoPago,
        fechaMovimiento = t.FechaMovimiento,
        fechaRegistro = t.FechaRegistro,
        idUsuarioRegistro = t.IdUsuarioRegistro,
        nombreUsuario = t.UsuarioRegistro?.NombreCompleto,
        idSesionCaja = t.IdSesionCaja,
        estado = t.Estado
    };
}
