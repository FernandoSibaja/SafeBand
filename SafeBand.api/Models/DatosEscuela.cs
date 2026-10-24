namespace SafeBand.api.Models;

/// <summary>
/// Datos de la escuela que usa la app (una sola fila, Id = 1). Los edita el director desde la página,
/// sin tocar el código: así el mismo sistema sirve en otra escuela.
/// </summary>
public class DatosEscuela
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Teléfono al que pueden llamar los padres (botón "Llamar a la escuela" y avisos de SOS).</summary>
    public string? Telefono { get; set; }
}
