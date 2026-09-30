namespace SafeBand.api.Models;

/// <summary>Área física de la escuela (salón, patio, tienda, entrada...).</summary>
public class Zona
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoZona Tipo { get; set; }
    public bool Activa { get; set; } = true;

    // Relación: una zona puede tener VARIOS nodos
    public List<Nodo> Nodos { get; set; } = [];
}

public enum TipoZona
{
    Salon,
    Patio,
    Tienda,
    Entrada,
    Perimetro,
    Otra
}
