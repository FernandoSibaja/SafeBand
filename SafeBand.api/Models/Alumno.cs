namespace SafeBand.api.Models;

/// <summary>Niño inscrito en la escuela que usa una pulsera.</summary>
public class Alumno
{
    public int Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string? ApellidoMaterno { get; set; }        // opcional: hay niños con un solo apellido
    public DateOnly? FechaNacimiento { get; set; }       // solo fecha, sin hora

    /// <summary>Número que la escuela ya asigna al alumno. Opcional pero único.</summary>
    public string? Matricula { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; }

    // Relación: un alumno puede tener varias pulseras a lo largo del tiempo (si pierde una, se le da otra)
    public List<Pulsera> Pulseras { get; set; } = [];

    // Relación: sus tutores (papá, mamá...)
    public List<TutorAlumno> Tutores { get; set; } = [];
}
