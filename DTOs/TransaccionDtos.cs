using System.ComponentModel.DataAnnotations;

namespace webapi_restful_backend_pulpi.DTOs;

public sealed class CrearTransaccionDto
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Concepto { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Monto { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una categoría válida.")]
    public int CategoriaId { get; init; }

    [Required, RegularExpression("^(Efectivo|Transferencia bancaria|Tarjeta|Depósito|Otro)$", ErrorMessage = "Selecciona un método de pago válido.")]
    public string MetodoPago { get; init; } = "Efectivo";

    public DateTime FechaMovimiento { get; init; } = DateTime.UtcNow;
}

public sealed record BalanceResumenDto(decimal TotalIngresos, decimal TotalGastos, decimal BalanceNeto, int TotalTransacciones);
