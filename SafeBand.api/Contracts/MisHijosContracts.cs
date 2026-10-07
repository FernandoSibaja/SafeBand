namespace SafeBand.api.Contracts;

/// <summary>Un hijo del padre que inició sesión, con dónde está y por dónde ha pasado.</summary>
public record MiHijoResponse(
    int AlumnoId,
    string Nombre,
    string? Parentesco,
    string? Pulsera,                 // identificador de su pulsera activa (null = no tiene)
    UltimaDeteccion? Ultima,         // null = todavía no se ha detectado
    List<TramoRecorrido> Recorrido); // por qué zonas pasó desde "desde"

/// <summary>La detección más reciente del hijo.</summary>
public record UltimaDeteccion(
    string Zona,
    int Rssi,
    bool Puesta,
    bool Sos,
    bool BateriaBaja,
    DateTime Timestamp);

/// <summary>Un tramo del recorrido: estuvo en esta zona de tal hora a tal hora.</summary>
public record TramoRecorrido(string Zona, DateTime Desde, DateTime Hasta);
