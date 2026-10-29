
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
public sealed class TransaccionesController : ControllerBase
{
    private readonly PulpiDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;

    public TransaccionesController(
        PulpiDbContext context,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _scopeFactory = scopeFactory;
    }

    // ============================================================
    // LISTAR TRANSACCIONES
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = _context.Transacciones
            .Where(t => t.Estado == "REGISTRADA")
            .AsNoTracking();

        if (!User.IsInRole("Administrador"))
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            query = query.Where(
                t => t.IdUsuarioRegistro == userId);
        }

        var items = await query
            .Include(t => t.Categoria)
            .Include(t => t.UsuarioRegistro)
            .OrderByDescending(t => t.FechaMovimiento)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        return Ok(items.Select(t => ToResponse(t)));
    }

    // ============================================================
    // OBTENER TRANSACCIÓN POR ID
    // ============================================================

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var query = _context.Transacciones
            .Include(t => t.Categoria)
            .Include(t => t.UsuarioRegistro)
            .Include(t => t.SesionCaja)
            .Where(t => t.Estado == "REGISTRADA");

        if (!User.IsInRole("Administrador"))
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            query = query.Where(
                t => t.IdUsuarioRegistro == userId);
        }

        var entity = await query.FirstOrDefaultAsync(
            t => t.Id == id);

        return entity is null
            ? NotFound(new
            {
                message = "Transacción no encontrada."
            })
            : Ok(ToResponse(entity));
    }

    // ============================================================
    // CREAR TRANSACCIÓN
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CrearTransaccionDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!IsTodayInNicaragua(dto.FechaMovimiento))
        {
            return BadRequest(new
            {
                message =
                    "La fecha de una nueva transacción debe ser " +
                    "la fecha actual de Nicaragua."
            });
        }

        var strategy =
            _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(
            async () =>
            {
                // Contexto independiente para este intento.
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PulpiDbContext>();

                await using var dbTransaction =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var category = await db.Categorias
                    .FirstOrDefaultAsync(c =>
                        c.IdCategoria == dto.CategoriaId &&
                        c.Activa);

                if (category is null)
                {
                    return new BadRequestObjectResult(new
                    {
                        message =
                            "La categoría seleccionada no existe " +
                            "o está desactivada."
                    });
                }

                var session = await db.SesionesCaja
                    .FirstOrDefaultAsync(s =>
                        s.IdUsuarioApertura == userId &&
                        s.Estado == "ABIERTA");

                if (session is null)
                {
                    return new ConflictObjectResult(new
                    {
                        message =
                            "Debes abrir una sesión de caja antes " +
                            "de registrar movimientos."
                    });
                }

                var entity = new Transaccion
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

                db.Transacciones.Add(entity);

                await db.SaveChangesAsync();

                await db.Entry(entity)
                    .Reference(t => t.UsuarioRegistro)
                    .LoadAsync();

                AuditoriaHelper.Registrar(
                    db,
                    userId,
                    "Transacciones",
                    entity.Id,
                    "CREAR",
                    despues: ToResponse(entity));

                await db.SaveChangesAsync();

                var response = ToResponse(entity);
                var entityId = entity.Id;

                await dbTransaction.CommitAsync();

                return new CreatedAtActionResult(
                    nameof(GetById),
                    null,
                    new { id = entityId },
                    response);
            });
    }

    // ============================================================
    // ACTUALIZAR TRANSACCIÓN
    // ============================================================

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] CrearTransaccionDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var isAdmin = User.IsInRole("Administrador");

        var strategy =
            _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(
            async () =>
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PulpiDbContext>();

                await using var dbTransaction =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                IQueryable<Transaccion> query =
                    db.Transacciones
                        .Include(t => t.Categoria)
                        .Include(t => t.UsuarioRegistro)
                        .Include(t => t.SesionCaja)
                        .Where(t => t.Estado == "REGISTRADA");

                if (!isAdmin)
                {
                    query = query.Where(
                        t => t.IdUsuarioRegistro == userId);
                }

                var entity = await query.FirstOrDefaultAsync(
                    t => t.Id == id);

                if (entity is null)
                {
                    return new NotFoundObjectResult(new
                    {
                        message = "Transacción no encontrada."
                    });
                }

                if (!isAdmin &&
                    entity.IdSesionCaja != null &&
                    entity.SesionCaja?.Estado != "ABIERTA")
                {
                    return new ConflictObjectResult(new
                    {
                        message =
                            "No puedes modificar un movimiento " +
                            "de una sesión de caja cerrada. " +
                            "Solicita una corrección al administrador."
                    });
                }

                var category = await db.Categorias
                    .FirstOrDefaultAsync(c =>
                        c.IdCategoria == dto.CategoriaId &&
                        c.Activa);

                if (category is null)
                {
                    return new BadRequestObjectResult(new
                    {
                        message =
                            "La categoría seleccionada no existe " +
                            "o está desactivada."
                    });
                }

                if (dto.FechaMovimiento.Date !=
                    entity.FechaMovimiento.Date)
                {
                    return new BadRequestObjectResult(new
                    {
                        message =
                            "La fecha de una transacción registrada " +
                            "no se puede modificar."
                    });
                }

                var before = ToResponse(entity);
                var affectedSessionId = entity.IdSesionCaja;

                entity.Concepto = dto.Concepto.Trim();
                entity.IdCategoria = category.IdCategoria;
                entity.Categoria = category;
                entity.Monto = dto.Monto;
                entity.MetodoPago = dto.MetodoPago;

                AuditoriaHelper.Registrar(
                    db,
                    userId,
                    "Transacciones",
                    entity.Id,
                    "ACTUALIZAR",
                    before,
                    ToResponse(entity));

                await db.SaveChangesAsync();

                await RecalculateClosedSessionAsync(
                    db,
                    affectedSessionId,
                    userId);

                await db.SaveChangesAsync();

                var response = ToResponse(entity);

                await dbTransaction.CommitAsync();

                return new OkObjectResult(response);
            });
    }

    // ============================================================
    // ANULAR TRANSACCIÓN
    // ============================================================

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        long id,
        [FromQuery] string? motivo = null)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var isAdmin = User.IsInRole("Administrador");

        var strategy =
            _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(
            async () =>
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PulpiDbContext>();

                await using var dbTransaction =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                IQueryable<Transaccion> query =
                    db.Transacciones
                        .Include(t => t.Categoria)
                        .Include(t => t.UsuarioRegistro)
                        .Include(t => t.SesionCaja)
                        .Where(t => t.Estado == "REGISTRADA");

                if (!isAdmin)
                {
                    query = query.Where(
                        t => t.IdUsuarioRegistro == userId);
                }

                var entity = await query.FirstOrDefaultAsync(
                    t => t.Id == id);

                if (entity is null)
                {
                    return new NotFoundObjectResult(new
                    {
                        message = "Transacción no encontrada."
                    });
                }

                if (!isAdmin &&
                    entity.IdSesionCaja != null &&
                    entity.SesionCaja?.Estado != "ABIERTA")
                {
                    return new ConflictObjectResult(new
                    {
                        message =
                            "No puedes anular un movimiento de una " +
                            "sesión cerrada. Solicita la corrección " +
                            "al administrador."
                    });
                }

                var before = ToResponse(entity);
                var affectedSessionId = entity.IdSesionCaja;

                entity.Estado = "ANULADA";

                AuditoriaHelper.Registrar(
                    db,
                    userId,
                    "Transacciones",
                    entity.Id,
                    "ANULAR",
                    before,
                    new
                    {
                        entity.Estado
                    },
                    string.IsNullOrWhiteSpace(motivo)
                        ? "Anulación solicitada desde el sistema"
                        : motivo);

                await db.SaveChangesAsync();

                await RecalculateClosedSessionAsync(
                    db,
                    affectedSessionId,
                    userId);

                await db.SaveChangesAsync();

                await dbTransaction.CommitAsync();

                return new NoContentResult();
            });
    }

    // ============================================================
    // RECALCULAR SALDO DE UNA SESIÓN CERRADA
    // ============================================================

    private static async Task RecalculateClosedSessionAsync(
        PulpiDbContext db,
        long? sessionId,
        int userId)
    {
        if (!sessionId.HasValue)
            return;

        var session = await db.SesionesCaja
            .FirstOrDefaultAsync(s =>
                s.IdSesionCaja == sessionId.Value &&
                s.Estado == "CERRADA");

        if (session is null)
            return;

        var before = new
        {
            session.SaldoEsperado
        };

        var cashMovements = db.Transacciones
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
            db,
            userId,
            "SesionesCaja",
            session.IdSesionCaja,
            "RECALCULAR_SALDO_ESPERADO",
            before,
            new
            {
                session.SaldoEsperado
            },
            "El administrador corrigió o anuló un movimiento " +
            "de una sesión ya cerrada.");
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

    private static DateTime GetNicaraguaToday()
    {
        var timeZoneId = OperatingSystem.IsWindows()
            ? "Central America Standard Time"
            : "America/Managua";

        var nicaraguaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo
            .ConvertTimeFromUtc(
                DateTime.UtcNow,
                nicaraguaTimeZone)
            .Date;
    }

    private static bool IsTodayInNicaragua(DateTime date)
    {
        return date.Date == GetNicaraguaToday();
    }

    private static object ToResponse(Transaccion t)
    {
        return new
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
}
