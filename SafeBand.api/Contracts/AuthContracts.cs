using System.ComponentModel.DataAnnotations;

namespace SafeBand.api.Contracts;

/// <summary>
/// JSON para iniciar sesión. Ejemplo:
/// { "email": "correo@uabc.edu.mx", "password": "...", "recordarme": true }
/// </summary>
public record LoginRequest(
    [property: Required(ErrorMessage = "El campo 'email' es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El campo 'email' no tiene un formato válido.")]
    string? Email,

    [property: Required(ErrorMessage = "El campo 'password' es obligatorio.")]
    string? Password,

    // true = la sesión sobrevive aunque se cierre el navegador (hasta que expire)
    bool Recordarme = false);

/// <summary>
/// JSON para que un padre cree su cuenta. Ejemplo:
/// { "email": "maria@correo.com", "codigo": "K7M4-9QPX", "password": "...", "aceptaAvisoPrivacidad": true }
/// </summary>
public record RegistroRequest(
    [property: Required(ErrorMessage = "El campo 'email' es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El campo 'email' no tiene un formato válido.")]
    string? Email,

    [property: Required(ErrorMessage = "El código de invitación es obligatorio.")]
    [property: StringLength(20, ErrorMessage = "El código de invitación no es válido.")]
    string? Codigo,

    [property: Required(ErrorMessage = "La contraseña es obligatoria.")]
    [property: StringLength(100, ErrorMessage = "La contraseña admite máximo 100 caracteres.")]
    string? Password,

    bool AceptaAvisoPrivacidad = false);

/// <summary>Quién inició sesión. Lo usa la página para decidir qué vista mostrar.</summary>
public record UsuarioActualResponse(
    string Id,
    string Email,
    string Nombre,
    List<string> Roles,
    int? TutorId);
