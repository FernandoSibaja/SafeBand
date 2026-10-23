using System.ComponentModel.DataAnnotations;

namespace SafeBand.api.Contracts;

/// <summary>
/// JSON para dar de alta o editar un grupo. Ejemplo:
/// { "nombre": "3°A", "zonaId": 4, "maestroId": "a1b2...", "alumnoIds": [1, 2, 3] }
/// alumnoIds: los alumnos que deben quedar en el grupo (si estaban en otro, se cambian).
/// null = no tocar los alumnos; [] = quitarlos a todos.
/// </summary>
public record GuardarGrupoRequest(
    [property: Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [property: StringLength(50, ErrorMessage = "El campo 'nombre' admite máximo 50 caracteres.")]
    string? Nombre,

    int? ZonaId = null,

    string? MaestroId = null,

    List<int>? AlumnoIds = null);

/// <summary>Un alumno dentro de la respuesta de un grupo.</summary>
public record AlumnoDeGrupo(int Id, string Nombre);

/// <summary>JSON que devuelve la API al consultar un grupo.</summary>
public record GrupoResponse(
    int Id,
    string Nombre,
    bool Activo,
    int? ZonaId,
    string? Zona,              // nombre del salón
    string? MaestroId,
    string? Maestro,           // nombre del maestro
    List<AlumnoDeGrupo> Alumnos);   // solo los activos, por apellido

/// <summary>Una cuenta con rol Maestro, para elegirla como titular de un grupo.</summary>
public record MaestroResumen(string Id, string Nombre, string? Email);

/// <summary>El grupo de un alumno (en la respuesta de alumnos).</summary>
public record GrupoResumen(int Id, string Nombre);
