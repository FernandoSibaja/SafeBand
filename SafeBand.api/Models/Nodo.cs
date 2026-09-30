namespace SafeBand.api.Models;

/// <summary>ESP32 receptor que escanea BLE y reporta las pulseras que detecta en su zona.</summary>
public class Nodo
{
    public int Id { get; set; }

    /// <summary>Identificador que el ESP32 envía en cada lectura, ej. "nodo-1".</summary>
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Relación: cada nodo pertenece a UNA zona
    public int ZonaId { get; set; }
    public Zona Zona { get; set; } = null!;

    /// <summary>RSSI mínimo (dBm) para considerar que la pulsera está dentro de la zona.</summary>
    public int UmbralRssi { get; set; } = -75;

    /// <summary>Nodo con batería para salidas escolares.</summary>
    public bool EsPortatil { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>Última vez que el nodo envió datos (para detectar nodos apagados).</summary>
    public DateTime? UltimoContacto { get; set; }
}
