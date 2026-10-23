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

    // Cómo se decide que un niño está en esta zona. null = automático según el tipo (ver PorDefecto).
    public ModoUbicacion? Ubicacion { get; set; }

    // Relación: una zona puede tener VARIOS nodos
    public List<Nodo> Nodos { get; set; } = [];

    /// <summary>
    /// Modo cuando la zona está en "automático": los cuartos donde se pone un nodo adentro (salón, tienda) van por nodo;
    /// los espacios abiertos y los cuartos chicos sin nodo (patio, entrada, perímetro, otra) van por triangulación.
    /// </summary>
    public static ModoUbicacion PorDefecto(TipoZona tipo) =>
        tipo is TipoZona.Salon or TipoZona.Tienda ? ModoUbicacion.PorNodo : ModoUbicacion.Triangulacion;
}

/// <summary>Cómo se ubica a un niño dentro de una zona.</summary>
public enum ModoUbicacion
{
    /// <summary>Hay un nodo adentro: si ese nodo es el que mejor oye la pulsera, el niño está aquí (los muros ayudan).</summary>
    PorNodo,
    /// <summary>Espacio abierto o sin nodo propio: se calcula el punto con varios nodos y se ve en qué zona cae.</summary>
    Triangulacion
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
