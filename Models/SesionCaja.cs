using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace webapi_restful_backend_pulpi.Models;

public sealed class SesionCaja
{
    [Key]
    public long IdSesionCaja { get; set; }

    public int IdUsuarioApertura { get; set; }
    public Usuario? UsuarioApertura { get; set; }

    public DateTime FechaHoraApertura { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SaldoInicial { get; set; }

    public int? IdUsuarioCierre { get; set; }
    public Usuario? UsuarioCierre { get; set; }
    public DateTime? FechaHoraCierre { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SaldoEsperado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SaldoDeclarado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Diferencia { get; set; }

    [Required, MaxLength(10)]
    public string Estado { get; set; } = "ABIERTA";

    [MaxLength(250)]
    public string? Observaciones { get; set; }

    public ICollection<Transaccion> Transacciones { get; set; } = new List<Transaccion>();
}
