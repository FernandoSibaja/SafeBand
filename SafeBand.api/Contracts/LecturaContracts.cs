using System.ComponentModel.DataAnnotations;

namespace SafeBand.api.Contracts;

/// <summary>
/// JSON que manda el ESP32 del nodo. Ejemplo:
/// { "nodo": "nodo-1", "pulsera": "SB-0001", "rssi": -67 }
/// </summary>
public record CrearLecturaRequest(
    [property: Required(ErrorMessage = "El campo 'nodo' es obligatorio.")]
    string? Nodo,

    [property: Required(ErrorMessage = "El campo 'pulsera' es obligatorio.")]
    string? Pulsera,

    [property: Required(ErrorMessage = "El campo 'rssi' es obligatorio.")]
    [property: Range(-127, 0, ErrorMessage = "El campo 'rssi' debe estar entre -127 y 0 dBm.")]
    int? Rssi,

    bool Puesta = true,
    bool Sos = false,
    bool BateriaBaja = false,

    // Opcional, con zona horaria (ej. "2026-09-29T18:30:00-06:00"). Si no llega, la API usa la hora actual.
    DateTimeOffset? Timestamp = null);

/// <summary>JSON que devuelve la API al guardar o consultar una lectura.</summary>
public record LecturaResponse(
    long Id,
    string Nodo,
    string Zona,
    string Pulsera,
    string? Alumno,
    int Rssi,
    string Unidad,
    bool Puesta,
    bool Sos,
    bool BateriaBaja,
    DateTime Timestamp);
