namespace SafeBand.api.Contracts;

/// <summary>Un alumno en el panel de la escuela (director): su grupo, el salón de su grupo y su última detección.</summary>
public record AlumnoEnEscuela(
    int Id,
    string Nombre,
    int? GrupoId,
    string? Grupo,
    int? SalonId,                    // zona del salón de su grupo (para saber si está en su salón)
    string? Pulsera,
    UltimaDeteccion? Ultima);        // null = todavía no se ha detectado
