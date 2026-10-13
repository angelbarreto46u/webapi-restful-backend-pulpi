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
public sealed class CategoriasController : ControllerBase
{
    private readonly PulpiDbContext _context;
    public CategoriasController(PulpiDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = _context.Categorias.AsNoTracking();
        if (!User.IsInRole("Administrador")) query = query.Where(c => c.Activa);
        var items = await query.OrderBy(c => c.Tipo).ThenBy(c => c.Nombre)
            .Select(c => new { c.IdCategoria, c.Nombre, c.Tipo, c.Descripcion, c.Activa, c.FechaCreacion })
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var category = await _context.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.IdCategoria == id);
        if (category is null || (!User.IsInRole("Administrador") && !category.Activa))
            return NotFound(new { message = "Categoría no encontrada." });
        return Ok(ToResponse(category));
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GuardarCategoriaDto dto)
    {
        var name = dto.Nombre.Trim();
        if (await _context.Categorias.AnyAsync(c => c.Nombre.ToLower() == name.ToLower() && c.Tipo == dto.Tipo))
            return Conflict(new { message = "Ya existe una categoría con ese nombre y tipo." });

        var category = new Categoria { Nombre = name, Tipo = dto.Tipo, Descripcion = (dto.Descripcion ?? string.Empty).Trim(), Activa = dto.Activa, FechaCreacion = DateTime.UtcNow };
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        _context.Categorias.Add(category);
        await _context.SaveChangesAsync();
        var userId = GetUserId();
        AuditoriaHelper.Registrar(_context, userId, "Categorias", category.IdCategoria, "CREAR",
            despues: ToResponse(category));
        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();
        return CreatedAtAction(nameof(GetById), new { id = category.IdCategoria }, ToResponse(category));
    }

    [Authorize(Roles = "Administrador")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] GuardarCategoriaDto dto)
    {
        var category = await _context.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id);
        if (category is null) return NotFound(new { message = "Categoría no encontrada." });
        var name = dto.Nombre.Trim();
        if (await _context.Categorias.AnyAsync(c => c.IdCategoria != id && c.Nombre.ToLower() == name.ToLower() && c.Tipo == dto.Tipo))
            return Conflict(new { message = "Ya existe otra categoría con ese nombre y tipo." });
        if (category.Tipo != dto.Tipo && await _context.Transacciones.AnyAsync(t => t.IdCategoria == id))
            return Conflict(new { message = "No se puede cambiar el tipo de una categoría que ya tiene transacciones. Crea otra categoría del tipo correcto." });

        var before = ToResponse(category);
        category.Nombre = name;
        category.Tipo = dto.Tipo;
        category.Descripcion = (dto.Descripcion ?? string.Empty).Trim();
        category.Activa = dto.Activa;
        AuditoriaHelper.Registrar(_context, GetUserId(), "Categorias", category.IdCategoria, "ACTUALIZAR", before, ToResponse(category));
        await _context.SaveChangesAsync();
        return Ok(ToResponse(category));
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id);
        if (category is null) return NotFound(new { message = "Categoría no encontrada." });
        if (!category.Activa) return NoContent();
        var before = ToResponse(category);
        category.Activa = false; // Desactivación lógica: conserva el historial financiero.
        AuditoriaHelper.Registrar(_context, GetUserId(), "Categorias", category.IdCategoria, "DESACTIVAR", before, ToResponse(category));
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private int? GetUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static object ToResponse(Categoria c) => new { c.IdCategoria, c.Nombre, c.Tipo, c.Descripcion, c.Activa, c.FechaCreacion };
}
