using System.ComponentModel.DataAnnotations;

namespace webapi_restful_backend_pulpi.Models;

public sealed class Usuario
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public int IdRol { get; set; }
    public Rol? Rol { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
