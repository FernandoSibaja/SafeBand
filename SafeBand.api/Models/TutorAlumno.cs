namespace SafeBand.api.Models;

/// <summary>
/// Vínculo tutor ↔ alumno (muchos a muchos): un tutor puede tener varios hijos
/// y un alumno puede tener varios tutores. Es lo que decide qué niños ve cada padre.
/// </summary>
public class TutorAlumno
{
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    public int AlumnoId { get; set; }
    public Alumno Alumno { get; set; } = null!;

    /// <summary>Ej. "Madre", "Padre", "Abuela", "Tutor legal".</summary>
    public string? Parentesco { get; set; }

    /// <summary>A quién se avisa primero ante una alerta.</summary>
    public bool EsContactoPrincipal { get; set; }

    /// <summary>Si puede recoger al alumno en la salida (se usará en la recogida autorizada).</summary>
    public bool PuedeRecoger { get; set; } = true;
}
