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

/// <summary>Quién inició sesión. Lo usa la página para decidir qué vista mostrar.</summary>
public record UsuarioActualResponse(
    string Id,
    string Email,
    string Nombre,
    List<string> Roles,
    int? TutorId);
