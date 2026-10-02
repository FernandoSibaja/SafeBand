using System.ComponentModel.DataAnnotations;

namespace SafeBand.api.Contracts;

/// <summary>
/// JSON para dar de alta o editar un tutor. Ejemplo:
/// { "nombres": "María", "apellidoPaterno": "García", "email": "maria@correo.com", "telefono": "686 123 4567" }
/// </summary>
public record GuardarTutorRequest(
    [property: Required(ErrorMessage = "El campo 'nombres' es obligatorio.")]
    [property: StringLength(100, ErrorMessage = "El campo 'nombres' admite máximo 100 caracteres.")]
    string? Nombres,

    [property: Required(ErrorMessage = "El campo 'apellidoPaterno' es obligatorio.")]
    [property: StringLength(100, ErrorMessage = "El campo 'apellidoPaterno' admite máximo 100 caracteres.")]
    string? ApellidoPaterno,

    [property: Required(ErrorMessage = "El campo 'email' es obligatorio.")]
    [property: EmailAddress(ErrorMessage = "El campo 'email' no tiene un formato válido.")]
    [property: StringLength(200, ErrorMessage = "El campo 'email' admite máximo 200 caracteres.")]
    string? Email,

    [property: StringLength(100, ErrorMessage = "El campo 'apellidoMaterno' admite máximo 100 caracteres.")]
    string? ApellidoMaterno = null,

    [property: RegularExpression(@"^[0-9+\s\-()]{7,20}$",
        ErrorMessage = "El campo 'telefono' solo admite números, espacios, +, - y paréntesis (7 a 20 caracteres).")]
    string? Telefono = null);

/// <summary>
/// JSON para vincular un tutor con un alumno (o actualizar el vínculo). Ejemplo:
/// { "parentesco": "Madre", "esContactoPrincipal": true, "puedeRecoger": true }
/// </summary>
public record VincularAlumnoRequest(
    [property: StringLength(50, ErrorMessage = "El campo 'parentesco' admite máximo 50 caracteres.")]
    string? Parentesco = null,
    bool EsContactoPrincipal = false,
    bool PuedeRecoger = true);

/// <summary>Un hijo dentro de la respuesta del tutor.</summary>
public record HijoResponse(
    int AlumnoId,
    string Nombre,
    string? Parentesco,
    bool EsContactoPrincipal,
    bool PuedeRecoger);

/// <summary>JSON que devuelve la API al consultar un tutor.</summary>
public record TutorResponse(
    int Id,
    string Nombres,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string Email,
    string? Telefono,
    bool Activo,
    DateTime FechaAlta,
    List<HijoResponse> Hijos);
