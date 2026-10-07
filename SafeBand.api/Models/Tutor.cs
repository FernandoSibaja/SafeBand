namespace SafeBand.api.Models;

/// <summary>
/// Padre, madre o tutor legal. Puede tener varios hijos (ver <see cref="TutorAlumno"/>).
/// Lo da de alta la escuela; después el tutor crea su cuenta con un código de invitación.
/// </summary>
public class Tutor
{
    public int Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string? ApellidoMaterno { get; set; }

    /// <summary>Correo con el que se registrará en la app. Único. Se guarda en minúsculas.</summary>
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; }

    // Relación: los alumnos de los que es tutor
    public List<TutorAlumno> Alumnos { get; set; } = [];

    // Códigos de invitación generados para que cree su cuenta
    public List<Invitacion> Invitaciones { get; set; } = [];
}
