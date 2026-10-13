using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace webapi_restful_backend_pulpi.Models;

public sealed class Transaccion
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Concepto { get; set; } = string.Empty;

    public int IdCategoria { get; set; }
    public Categoria? Categoria { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Monto { get; set; }

    [Required, MaxLength(50)]
    public string MetodoPago { get; set; } = "Efectivo";

    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public int IdUsuarioRegistro { get; set; }
    public Usuario? UsuarioRegistro { get; set; }

    // Nullable únicamente para conservar movimientos históricos anteriores a la función de caja.
    public long? IdSesionCaja { get; set; }
    public SesionCaja? SesionCaja { get; set; }

    [Required, MaxLength(12)]
    public string Estado { get; set; } = "REGISTRADA";
}
