using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webapi_restful_backend_pulpi.Data;
using webapi_restful_backend_pulpi.DTOs;

namespace webapi_restful_backend_pulpi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public sealed class BalancesController : ControllerBase
{
    private readonly PulpiDbContext _context;
    public BalancesController(PulpiDbContext context) => _context = context;

    [HttpGet("resumen")]
    public async Task<ActionResult<BalanceResumenDto>> GetResumen()
    {
        var query = _context.Transacciones.Where(t => t.Estado == "REGISTRADA");
        var income = await query.Where(t => t.Categoria!.Tipo == "Ingreso").SumAsync(t => (decimal?)t.Monto) ?? 0m;
        var expenses = await query.Where(t => t.Categoria!.Tipo == "Gasto").SumAsync(t => (decimal?)t.Monto) ?? 0m;
        var count = await query.CountAsync();
        return Ok(new BalanceResumenDto(income, expenses, income - expenses, count));
    }

    [HttpGet("caja-efectivo")]
    public async Task<IActionResult> GetCashOnHand()
    {
        var openSessions = await _context.SesionesCaja.Where(s => s.Estado == "ABIERTA").ToListAsync();
        var results = new List<object>();
        foreach (var session in openSessions)
        {
            var query = _context.Transacciones.Include(t => t.Categoria)
                .Where(t => t.IdSesionCaja == session.IdSesionCaja && t.Estado == "REGISTRADA" && t.MetodoPago == "Efectivo");
            var income = await query.Where(t => t.Categoria!.Tipo == "Ingreso").SumAsync(t => (decimal?)t.Monto) ?? 0m;
            var expenses = await query.Where(t => t.Categoria!.Tipo == "Gasto").SumAsync(t => (decimal?)t.Monto) ?? 0m;
            results.Add(new { session.IdSesionCaja, session.IdUsuarioApertura, session.SaldoInicial, ingresosEfectivo = income, egresosEfectivo = expenses, saldoEsperado = session.SaldoInicial + income - expenses });
        }
        return Ok(results);
    }
}
