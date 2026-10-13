using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webapi_restful_backend_pulpi.Data;

namespace webapi_restful_backend_pulpi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public sealed class AuditoriaController : ControllerBase
{
    private readonly PulpiDbContext _context;
    public AuditoriaController(PulpiDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limite = 200)
    {
        limite = Math.Clamp(limite, 1, 500);
        var items = await _context.AuditoriaAcciones.AsNoTracking().Include(a => a.Usuario)
            .OrderByDescending(a => a.FechaHora).ThenByDescending(a => a.IdAuditoria).Take(limite)
            .Select(a => new { a.IdAuditoria, a.Entidad, a.IdRegistro, a.Accion, a.FechaHora, a.DatosAnteriores, a.DatosNuevos, a.Motivo, nombreUsuario = a.Usuario == null ? "Usuario eliminado" : a.Usuario.NombreCompleto, username = a.Usuario == null ? "" : a.Usuario.Username })
            .ToListAsync();
        return Ok(items);
    }
}
