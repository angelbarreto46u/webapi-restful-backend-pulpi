using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webapi_restful_backend_pulpi.Data;

namespace webapi_restful_backend_pulpi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public sealed class ReportesController : ControllerBase
{
    private readonly PulpiDbContext _context;
    public ReportesController(PulpiDbContext context) => _context = context;

    [HttpGet("resumen")]
    public async Task<IActionResult> Summary([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        if (desde == default || hasta == default || desde > hasta)
            return BadRequest(new { message = "El rango de fechas es obligatorio y la fecha inicial no puede superar la fecha final." });

        var start = desde.ToDateTime(TimeOnly.MinValue);
        var endExclusive = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var query = _context.Transacciones.Include(t => t.Categoria)
            .Where(t => t.Estado == "REGISTRADA" && t.FechaMovimiento >= start && t.FechaMovimiento < endExclusive);
        var incomes = await query.Where(t => t.Categoria!.Tipo == "Ingreso").SumAsync(t => (decimal?)t.Monto) ?? 0m;
        var expenses = await query.Where(t => t.Categoria!.Tipo == "Gasto").SumAsync(t => (decimal?)t.Monto) ?? 0m;
        var count = await query.CountAsync();
        var byCategory = await query.GroupBy(t => new { t.IdCategoria, t.Categoria!.Nombre, t.Categoria.Tipo })
            .Select(g => new { g.Key.IdCategoria, g.Key.Nombre, g.Key.Tipo, Total = g.Sum(t => t.Monto), Cantidad = g.Count() })
            .OrderByDescending(g => g.Total).ToListAsync();
        return Ok(new { desde, hasta, totalIngresos = incomes, totalEgresos = expenses, flujoNeto = incomes - expenses, cantidadMovimientos = count, categorias = byCategory });
    }
}
