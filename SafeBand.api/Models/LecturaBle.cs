namespace SafeBand.api.Models;

/// <summary>
/// Una detección: el nodo X vio la pulsera Y con cierto RSSI y estado.
/// Es el dato base del sistema (ubicación por zona, alertas, pase de lista).
/// </summary>
public class LecturaBle
{
    // long en vez de int: esta tabla crecerá mucho más que las demás
    public long Id { get; set; }

    // Qué nodo hizo la detección
    public int NodoId { get; set; }
    public Nodo Nodo { get; set; } = null!;

    // Qué pulsera fue detectada
    public int PulseraId { get; set; }
    public Pulsera Pulsera { get; set; } = null!;

    // Qué alumno traía la pulsera EN ESE MOMENTO (se copia al recibir la lectura).
    // Así el historial no cambia si después la pulsera se le da a otro niño.
    // null = la pulsera no estaba asignada a nadie.
    public int? AlumnoId { get; set; }
    public Alumno? Alumno { get; set; }

    /// <summary>Intensidad de señal en dBm (negativo; más cerca de 0 = más cerca del nodo).</summary>
    public int Rssi { get; set; }

    // Estado que la pulsera manda dentro de su anuncio BLE
    public bool Puesta { get; set; } = true;   // reed switch: ¿está cerrada en la muñeca?
    public bool Sos { get; set; }              // botón de pánico presionado
    public bool BateriaBaja { get; set; }      // voltaje de la LiPo bajo el umbral

    /// <summary>Hora de la detección en UTC (la manda el ESP32; si no, la pone la API).</summary>
    public DateTime Timestamp { get; set; }
}
