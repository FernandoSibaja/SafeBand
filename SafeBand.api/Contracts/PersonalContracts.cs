using System.ComponentModel.DataAnnotations;

namespace SafeBand.api.Contracts;

/// <summary>
/// JSON para dar de alta o editar una cuenta del personal de la escuela. Ejemplo:
/// { "nombre": "Laura Méndez", "email": "laura@escuela.mx", "rol": "Maestro" }
/// La cuenta se crea sin contraseña: la persona la pone al usar su código de invitación.
/// </summary>
public record GuardarPersonalRequest(
    [property: Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [property: StringLength(150, ErrorMessage = "El campo 'nombre' admite máximo 150 caracteres.")]
    string? Nombre,

    [property: Required(ErrorMessage = "El campo 'email' es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El campo 'email' no tiene un formato válido.")]
    [property: StringLength(200, ErrorMessage = "El campo 'email' admite máximo 200 caracteres.")]
    string? Email,

    [property: Required(ErrorMessage = "El campo 'rol' es obligatorio (Maestro o Administrador).")]
    [property: RegularExpression("^(Maestro|Administrador)$", ErrorMessage = "El campo 'rol' debe ser Maestro o Administrador.")]
    string? Rol);

/// <summary>JSON que devuelve la API al consultar una cuenta del personal.</summary>
public record PersonalResponse(
    string Id,
    string Nombre,
    string Email,
    string Rol,
    bool Activo,
    string EstadoCuenta,        // SinInvitar, InvitacionPendiente o CuentaActiva (como en tutores)
    List<string> Grupos,        // grupos activos donde es maestro titular
    bool EresTu);               // la cuenta con la que se inició sesión (no se puede dar de baja ni quitarse el rol)
