using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace webapi_restful_backend_pulpi.Models;

public sealed class AuditoriaAccion
{
    [Key]
    public long IdAuditoria { get; set; }

    public int? IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }

    [Required, MaxLength(50)]
    public string Entidad { get; set; } = string.Empty;

    public long? IdRegistro { get; set; }

    [Required, MaxLength(30)]
    public string Accion { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "nvarchar(max)")]
    public string? DatosAnteriores { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? DatosNuevos { get; set; }

    [MaxLength(250)]
    public string? Motivo { get; set; }
}
