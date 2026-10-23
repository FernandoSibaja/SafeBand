namespace SafeBand.api.Contracts;

/// <summary>Un grupo del maestro que inició sesión, con dónde está cada uno de sus alumnos.</summary>
public record MiGrupoResponse(
    int Id,
    string Nombre,
    int? ZonaId,                     // su salón (para contar quién está en el salón)
    string? Zona,
    List<AlumnoDeMiGrupo> Alumnos);  // activos, por apellido

/// <summary>Un alumno del grupo y su última detección (la misma que ve su padre en "Mis hijos").</summary>
public record AlumnoDeMiGrupo(
    int Id,
    string Nombre,
    string? Pulsera,                 // identificador de su pulsera activa (null = no tiene)
    UltimaDeteccion? Ultima);        // null = todavía no se ha detectado
