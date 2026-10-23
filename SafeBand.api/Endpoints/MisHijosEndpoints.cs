using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;
using SafeBand.api.Servicios;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Lo que ve un padre: SOLO sus hijos (los que la escuela vinculó a su registro de Tutor).
/// El filtro se hace aquí, en la API: aunque alguien llame la API a mano, no recibe datos de otros niños.
/// </summary>
public static class MisHijosEndpoints
{
    // Si entre dos lecturas de la misma zona pasan más de esto, se considera que salió y volvió (tramo nuevo)
    private static readonly TimeSpan HuecoMaximo = TimeSpan.FromMinutes(10);
    private const int MaxLecturasRecorrido = 5000;

    public static void MapMisHijos(this WebApplication app)
    {
        app.MapGet("/api/mis-hijos", MisHijos)
            .RequireAuthorization(Politicas.SoloPadre)
            .WithTags("Familia")
            .WithSummary("Los hijos del padre con sesión: dónde están y su recorrido desde ?desde= (por defecto, las últimas 12 h)");
    }

    // GET /api/mis-hijos?desde=2026-10-07T07:00:00Z
    private static async Task<IResult> MisHijos(
        ClaimsPrincipal yo, UserManager<Usuario> usuarios, AppDbContext db, IConfiguration config, DateTimeOffset? desde)
    {
        var usuario = await usuarios.GetUserAsync(yo);
        if (usuario?.TutorId is null) return Results.Ok(new List<MiHijoResponse>());

        // La página manda "desde" = medianoche de hoy en su hora local
        var desdeUtc = desde?.UtcDateTime ?? DateTime.UtcNow.AddHours(-12);

        var hijos = await db.TutoresAlumnos.AsNoTracking()
            .Where(ta => ta.TutorId == usuario.TutorId && ta.Alumno.Activo)
            .OrderBy(ta => ta.Alumno.Nombres)
            .Select(ta => new
            {
                ta.AlumnoId,
                Nombre = ta.Alumno.Nombres + " " + ta.Alumno.ApellidoPaterno,
                ta.Parentesco,
                Pulsera = ta.Alumno.Pulseras.Where(p => p.Activa).Select(p => p.IdentificadorBle).FirstOrDefault(),
            })
            .ToListAsync();

        // Calibración de la prueba de alcance y tamaño del plano (para que el punto no quede fuera del edificio)
        var calibracion = new Ubicacion.Calibracion(
            config.GetValue("Ubicacion:Rssi1m", -84.3), config.GetValue("Ubicacion:N", 1.49));
        // Zonas activas con su modo de ubicación ("por nodo" o "por triangulación") y su rectángulo
        var filasZonas = await db.Zonas.AsNoTracking().Where(z => z.Activa)
            .Select(z => new { z.Id, z.Nombre, z.Tipo, z.Ubicacion, TieneNodos = z.Nodos.Any(n => n.Activo),
                z.PlanoX, z.PlanoY, z.PlanoAncho, z.PlanoAlto })
            .ToListAsync();
        var zonas = filasZonas.ToDictionary(z => z.Id, z => new Ubicacion.ZonaInfo(z.Id, z.Nombre,
            z.Ubicacion ?? Zona.PorDefecto(z.Tipo), z.TieneNodos, z.PlanoX, z.PlanoY, z.PlanoAncho, z.PlanoAlto));
        var dibujadas = zonas.Values.Where(z => z.X is not null).ToList();
        double? anchoPlano = dibujadas.Count == 0 ? null : dibujadas.Max(z => z.X!.Value + z.Ancho!.Value);
        double? altoPlano = dibujadas.Count == 0 ? null : dibujadas.Max(z => z.Y!.Value + z.Alto!.Value);

        var resultado = new List<MiHijoResponse>();
        foreach (var h in hijos)
        {
            // Última detección: con las lecturas de los últimos segundos (varios nodos pueden oírla a la vez) se decide
            // la zona por nodo o por triangulación (Ubicacion.Decidir). Usa el AlumnoId guardado en cada lectura: historial fiel.
            var horaUltima = await db.LecturasBle.AsNoTracking()
                .Where(l => l.AlumnoId == h.AlumnoId)
                .MaxAsync(l => (DateTime?)l.Timestamp);
            UltimaDeteccion? ultima = null;
            if (horaUltima is not null)
            {
                var recientes = await Lecturas(db, h.AlumnoId, horaUltima.Value - Ubicacion.Simultaneas);
                var l = Ubicacion.MasFuerte(recientes);
                var d = Ubicacion.Decidir(recientes, zonas, calibracion, anchoPlano, altoPlano);
                var p = d.Posicion;
                ultima = new UltimaDeteccion(d.ZonaId, d.Zona, l.Rssi, l.Puesta, l.Sos, l.BateriaBaja, l.Hora, d.Cerca, d.Metodo,
                    p is null ? null : new PosicionEstimada(p.X, p.Y, p.Radio, p.Nodos));
            }

            // Recorrido: por cada momento, la zona decidida con la misma regla que la última detección
            var delPeriodo = await Lecturas(db, h.AlumnoId, desdeUtc, MaxLecturasRecorrido);
            var recorrido = ArmarRecorrido(Ubicacion.Bloques(delPeriodo)
                .Select(b => (Ubicacion.Decidir(b, zonas, calibracion, anchoPlano, altoPlano).Zona, b.Max(l => l.Hora))));

            resultado.Add(new MiHijoResponse(h.AlumnoId, h.Nombre, h.Parentesco, h.Pulsera, ultima, recorrido));
        }
        return Results.Ok(resultado);
    }

    // Lecturas de un alumno desde una hora, en orden, con lo necesario para decidir la zona
    private static async Task<List<Ubicacion.Lectura>> Lecturas(AppDbContext db, int alumnoId, DateTime desde, int maximo = 200)
    {
        var filas = await db.LecturasBle.AsNoTracking()
            .Where(l => l.AlumnoId == alumnoId && l.Timestamp >= desde)
            .OrderBy(l => l.Timestamp)
            .Take(maximo)
            .Select(l => new { l.Timestamp, l.Nodo.ZonaId, Zona = l.Nodo.Zona.Nombre, l.Rssi, l.Nodo.UmbralRssi, l.Puesta, l.Sos, l.BateriaBaja,
                l.NodoId, l.Nodo.PlanoX, l.Nodo.PlanoY })
            .ToListAsync();
        return filas.Select(f => new Ubicacion.Lectura(DateTime.SpecifyKind(f.Timestamp, DateTimeKind.Utc),
            f.ZonaId, f.Zona, f.Rssi, f.UmbralRssi, f.Puesta, f.Sos, f.BateriaBaja, f.NodoId, f.PlanoX, f.PlanoY)).ToList();
    }

    /// <summary>
    /// Junta lecturas seguidas de la misma zona en un solo tramo:
    /// [Salón 8:00, Salón 8:01, Patio 10:30, Patio 10:40] → [Salón 8:00–8:01, Patio 10:30–10:40]
    /// </summary>
    private static List<TramoRecorrido> ArmarRecorrido(IEnumerable<(string Zona, DateTime Hora)> lecturas)
    {
        var tramos = new List<TramoRecorrido>();
        foreach (var (zona, hora) in lecturas)
        {
            var ultimo = tramos.Count > 0 ? tramos[^1] : null;
            if (ultimo is not null && ultimo.Zona == zona && hora - ultimo.Hasta <= HuecoMaximo)
                tramos[^1] = ultimo with { Hasta = hora };
            else
                tramos.Add(new TramoRecorrido(zona, hora, hora));
        }
        return tramos;
    }
}
