using System.ComponentModel.DataAnnotations;

namespace webapi_restful_backend_pulpi.Models;

public sealed class Categoria
{
    [Key]
    public int IdCategoria { get; set; }

    [Required, MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string Tipo { get; set; } = "Ingreso";

    [MaxLength(150)]
    public string Descripcion { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public ICollection<Transaccion> Transacciones { get; set; } = new List<Transaccion>();
}
