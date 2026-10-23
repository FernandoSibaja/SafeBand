namespace SafeBand.api.Models;

/// <summary>Grupo escolar (3°A, 5°B...): sus alumnos, su salón y su maestro titular.</summary>
public class Grupo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    /// <summary>Salón donde toma clases (una zona). Lo usarán "Mi grupo" y el pase de lista.</summary>
    public int? ZonaId { get; set; }
    public Zona? Zona { get; set; }

    /// <summary>Cuenta del maestro titular (rol Maestro). Ve a este grupo en "Mi grupo".</summary>
    public string? MaestroId { get; set; }
    public Usuario? Maestro { get; set; }

    // Relación: un grupo tiene VARIOS alumnos; un alumno está en un solo grupo
    public List<Alumno> Alumnos { get; set; } = [];
}
