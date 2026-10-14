namespace SafeBand.api.Models;

/// <summary>Área física de la escuela (salón, patio, tienda, entrada...).</summary>
public class Zona
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoZona Tipo { get; set; }
    public bool Activa { get; set; } = true;

    // Rectángulo de la zona en el plano de la escuela, en METROS.
    // (0, 0) es la esquina superior izquierda del plano; X crece a la derecha y Y hacia abajo.
    // Los cuatro son opcionales: una zona sin rectángulo no se dibuja en el plano.
    // En el paso de triangulación, los nodos usarán este mismo sistema de coordenadas.
    public double? PlanoX { get; set; }
    public double? PlanoY { get; set; }
    public double? PlanoAncho { get; set; }
    public double? PlanoAlto { get; set; }

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
