using System.ComponentModel.DataAnnotations;

namespace SafeBand.api.Contracts;

/// <summary>
/// JSON para dar de alta o editar un alumno. Ejemplo:
/// { "nombres": "Ana Sofía", "apellidoPaterno": "López", "apellidoMaterno": "García",
///   "fechaNacimiento": "2018-05-14", "matricula": "A-0001" }
/// </summary>
public record GuardarAlumnoRequest(
    [property: Required(ErrorMessage = "El campo 'nombres' es obligatorio.")]
    [property: StringLength(100, ErrorMessage = "El campo 'nombres' admite máximo 100 caracteres.")]
    string? Nombres,

    [property: Required(ErrorMessage = "El campo 'apellidoPaterno' es obligatorio.")]
    [property: StringLength(100, ErrorMessage = "El campo 'apellidoPaterno' admite máximo 100 caracteres.")]
    string? ApellidoPaterno,

    [property: StringLength(100, ErrorMessage = "El campo 'apellidoMaterno' admite máximo 100 caracteres.")]
    string? ApellidoMaterno = null,

    DateOnly? FechaNacimiento = null,

    [property: StringLength(30, ErrorMessage = "El campo 'matricula' admite máximo 30 caracteres.")]
    string? Matricula = null);

/// <summary>JSON que devuelve la API al consultar un alumno.</summary>
public record AlumnoResponse(
    int Id,
    string Nombres,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    DateOnly? FechaNacimiento,
    string? Matricula,
    bool Activo,
    DateTime FechaAlta,
    List<string> Pulseras);   // identificadores BLE de sus pulseras activas
