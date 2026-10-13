using System.ComponentModel.DataAnnotations;

namespace webapi_restful_backend_pulpi.Models;

public sealed class Rol
{
    [Key]
    public int IdRol { get; set; }

    [Required, MaxLength(30)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Descripcion { get; set; } = string.Empty;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
