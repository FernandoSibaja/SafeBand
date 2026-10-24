using SafeBand.api.Models;

namespace SafeBand.api.Contracts;

/// <summary>Una alerta como la ve la escuela (y el padre, solo las de sus hijos).</summary>
public record AlertaResponse(
    int Id,
    TipoAlerta Tipo,
    int AlumnoId,
    string Alumno,
    string? Grupo,
    string? Zona,                          // dónde estaba al activarse
    DateTime Inicio,
    DateTime? Fin,                         // null = activa
    OrigenDesactivacion? DesactivadaDesde, // Pulsera o Pagina
    string? DesactivadaPor,                // nombre de quien la apagó desde la página
    bool PuedoDesactivar);                 // la cuenta con sesión puede apagarla (director o maestro titular)
