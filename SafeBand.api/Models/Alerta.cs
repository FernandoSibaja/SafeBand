namespace SafeBand.api.Models;

/// <summary>
/// Aviso que el sistema genera solo y que queda ABIERTO hasta que se desactiva (Fin == null = activa).
/// Por ahora solo SOS: el niño mantiene presionado el botón, la pulsera lo anuncia unos segundos
/// y la alerta sigue activa en el sistema aunque la pulsera ya no lo mande.
/// </summary>
public class Alerta
{
    public int Id { get; set; }
    public TipoAlerta Tipo { get; set; }

    public int AlumnoId { get; set; }
    public Alumno Alumno { get; set; } = null!;

    /// <summary>Zona del nodo que recibió el aviso (dónde estaba al pedir ayuda).</summary>
    public int? ZonaId { get; set; }
    public Zona? Zona { get; set; }

    public DateTime Inicio { get; set; }

    /// <summary>Cuándo se desactivó. null = sigue activa.</summary>
    public DateTime? Fin { get; set; }

    /// <summary>Desde dónde se desactivó: la misma pulsera o la página.</summary>
    public OrigenDesactivacion? DesactivadaDesde { get; set; }

    /// <summary>Si se desactivó desde la página, la cuenta que lo hizo.</summary>
    public string? DesactivadaPorId { get; set; }
    public Usuario? DesactivadaPor { get; set; }
}

public enum TipoAlerta
{
    Sos,
}

public enum OrigenDesactivacion
{
    Pulsera,
    Pagina,
}
