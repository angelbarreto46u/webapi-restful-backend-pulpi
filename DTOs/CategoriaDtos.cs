using System.ComponentModel.DataAnnotations;

namespace webapi_restful_backend_pulpi.DTOs;

public sealed class GuardarCategoriaDto
{
    [Required, StringLength(60, MinimumLength = 2)]
    [RegularExpression(@"^[\p{L}\p{M}0-9][\p{L}\p{M}0-9 '\-()./&]*$", ErrorMessage = "El nombre de categoría contiene caracteres no permitidos.")]
    public string Nombre { get; init; } = string.Empty;

    [Required, RegularExpression("^(Ingreso|Gasto)$", ErrorMessage = "El tipo debe ser Ingreso o Gasto.")]
    public string Tipo { get; init; } = "Ingreso";

    [StringLength(150)]
    public string Descripcion { get; init; } = string.Empty;

    public bool Activa { get; init; } = true;
}

public sealed class AbrirSesionCajaDto
{
    [Required, Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El saldo inicial no puede ser negativo.")]
    public decimal? SaldoInicial { get; init; }
    [StringLength(250)]
    public string? Observaciones { get; init; }
}

public sealed class CerrarSesionCajaDto
{
    [Required, Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El efectivo contado no puede ser negativo.")]
    public decimal? SaldoDeclarado { get; init; }
    [StringLength(250)]
    public string? Observaciones { get; init; }
}
