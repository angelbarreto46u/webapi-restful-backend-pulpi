using System.ComponentModel.DataAnnotations;

namespace webapi_restful_backend_pulpi.DTOs;

public sealed class LoginDto
{
    [Required(ErrorMessage = "El nombre de usuario es obligatorio."), StringLength(50)]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria."), StringLength(100)]
    public string Password { get; init; } = string.Empty;
}

public sealed class RegistroDto
{
    [Required(ErrorMessage = "El nombre completo es obligatorio."), StringLength(150, MinimumLength = 2)]
    [RegularExpression(@"^[\p{L}\p{M}]+(?:[ '\-][\p{L}\p{M}]+)*$", ErrorMessage = "El nombre solo puede contener letras, espacios, apóstrofes o guiones.")]
    public string NombreCompleto { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [RegularExpression("^[a-zA-Z0-9._]{3,24}$", ErrorMessage = "El usuario debe tener entre 3 y 24 caracteres: letras, números, punto o guion bajo.")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio."), EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido."), StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria."), StringLength(72, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 72 caracteres.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Debe confirmar la contraseña."), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; init; } = string.Empty;
}

public sealed class ActualizarPerfilDto
{
    [Required, StringLength(150, MinimumLength = 2)]
    [RegularExpression(@"^[\p{L}\p{M}]+(?:[ '\-][\p{L}\p{M}]+)*$", ErrorMessage = "El nombre solo puede contener letras, espacios, apóstrofes o guiones.")]
    public string NombreCompleto { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; init; } = string.Empty;
}

public sealed class CambiarPasswordDto
{
    [Required]
    public string PasswordActual { get; init; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; init; } = string.Empty;
}

public sealed class CambiarRolDto
{
    [Required, RegularExpression("^(Administrador|Cajero)$", ErrorMessage = "El rol debe ser Administrador o Cajero.")]
    public string Rol { get; init; } = string.Empty;
}

public sealed record AuthResponseDto(int Id, string Token, string Username, string Email, string NombreCompleto, string Rol, DateTime FechaRegistro);
public sealed record UsuarioDto(int Id, string Username, string Email, string NombreCompleto, string Rol, DateTime FechaRegistro, bool Activo = true);

public sealed class CambiarEstadoUsuarioDto
{
    [Required]
    public bool? Activo { get; init; }
}
