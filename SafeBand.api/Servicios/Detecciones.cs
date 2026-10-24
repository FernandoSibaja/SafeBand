using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Servicios;

/// <summary>
/// Consultas para saber dónde está un alumno: carga lo que necesita Ubicacion.Decidir (zonas, calibración,
/// tamaño del plano) y arma su última detección y su recorrido. Lo usan "Mis hijos" (padre) y "Mi grupo" (maestro),
/// así los dos ven exactamente lo mismo.
/// </summary>
public static class Detecciones
{
    /// <summary>
    /// Zonas con su modo de ubicación, calibración de la prueba de alcance, tamaño del plano y
    /// los alumnos con SOS activo (el SOS se muestra mientras su alerta esté abierta, no solo mientras la pulsera lo mande).
    /// </summary>
    public record Contexto(IReadOnlyDictionary<int, Ubicacion.ZonaInfo> Zonas, Ubicacion.Calibracion Calibracion,
        double? AnchoPlano, double? AltoPlano, HashSet<int> ConSos);

    /// <summary>Se carga una vez por petición y sirve para todos los alumnos.</summary>
    public static async Task<Contexto> CargarContexto(AppDbContext db, IConfiguration config)
    {
        var calibracion = new Ubicacion.Calibracion(
            config.GetValue("Ubicacion:Rssi1m", -84.3), config.GetValue("Ubicacion:N", 1.49));
        // Zonas activas con su modo de ubicación ("por nodo" o "por triangulación") y su rectángulo
        var filas = await db.Zonas.AsNoTracking().Where(z => z.Activa)
            .Select(z => new { z.Id, z.Nombre, z.Tipo, z.Ubicacion, TieneNodos = z.Nodos.Any(n => n.Activo),
                z.PlanoX, z.PlanoY, z.PlanoAncho, z.PlanoAlto })
            .ToListAsync();
        var zonas = filas.ToDictionary(z => z.Id, z => new Ubicacion.ZonaInfo(z.Id, z.Nombre,
            z.Ubicacion ?? Zona.PorDefecto(z.Tipo), z.TieneNodos, z.PlanoX, z.PlanoY, z.PlanoAncho, z.PlanoAlto));
        // Tamaño del plano, para que el punto no quede fuera del edificio
        var dibujadas = zonas.Values.Where(z => z.X is not null).ToList();
        double? ancho = dibujadas.Count == 0 ? null : dibujadas.Max(z => z.X!.Value + z.Ancho!.Value);
        double? alto = dibujadas.Count == 0 ? null : dibujadas.Max(z => z.Y!.Value + z.Alto!.Value);
        return new Contexto(zonas, calibracion, ancho, alto, await Alertas.ConSosActivo(db));
    }

    /// <summary>
    /// Última detección de varios alumnos con UNA consulta (para un grupo o toda la escuela):
    /// por cada alumno, sus lecturas desde (su lectura más reciente − 15 s). Los que nunca se han detectado no aparecen.
    /// </summary>
    public static async Task<Dictionary<int, UltimaDeteccion>> Ultimas(AppDbContext db, List<int> alumnoIds, Contexto ctx)
    {
        if (alumnoIds.Count == 0) return [];
        var segundos = -Ubicacion.Simultaneas.TotalSeconds;
        var masRecientes = db.LecturasBle
            .Where(l => l.AlumnoId != null && alumnoIds.Contains(l.AlumnoId.Value))
            .GroupBy(l => l.AlumnoId)
            .Select(g => new { AlumnoId = g.Key, Max = g.Max(l => l.Timestamp) });
        var lecturas = db.LecturasBle.AsNoTracking()
            .Join(masRecientes, l => l.AlumnoId, m => m.AlumnoId, (l, m) => new { l, m.Max })
            .Where(x => x.l.Timestamp >= x.Max.AddSeconds(segundos))
            .Select(x => x.l);
        var filas = await Proyectar(lecturas).ToListAsync();

        var resultado = new Dictionary<int, UltimaDeteccion>();
        foreach (var grupo in filas.GroupBy(f => f.AlumnoId!.Value))
            resultado[grupo.Key] = Armar(grupo.Key, grupo.Select(ALectura).OrderBy(l => l.Hora).ToList(), ctx);
        return resultado;
    }

    /// <summary>
    /// Última detección: con las lecturas de los últimos segundos (varios nodos pueden oírla a la vez) se decide
    /// la zona por nodo o por triangulación (Ubicacion.Decidir). Usa el AlumnoId guardado en cada lectura: historial fiel.
    /// null = nunca se ha detectado.
    /// </summary>
    public static async Task<UltimaDeteccion?> Ultima(AppDbContext db, int alumnoId, Contexto ctx)
    {
        var horaUltima = await db.LecturasBle.AsNoTracking()
            .Where(l => l.AlumnoId == alumnoId)
            .MaxAsync(l => (DateTime?)l.Timestamp);
        if (horaUltima is null) return null;

        return Armar(alumnoId, await Lecturas(db, alumnoId, horaUltima.Value - Ubicacion.Simultaneas), ctx);
    }

    // Lecturas simultáneas → zona decidida + estado de la pulsera (de la más fuerte; batería de cualquiera).
    // SOS = tiene una alerta de SOS abierta.
    private static UltimaDeteccion Armar(int alumnoId, List<Ubicacion.Lectura> recientes, Contexto ctx)
    {
        var l = Ubicacion.MasFuerte(recientes);
        var d = Ubicacion.Decidir(recientes, ctx.Zonas, ctx.Calibracion, ctx.AnchoPlano, ctx.AltoPlano);
        var p = d.Posicion;
        return new UltimaDeteccion(d.ZonaId, d.Zona, l.Rssi, l.Puesta, ctx.ConSos.Contains(alumnoId), l.BateriaBaja, l.Hora, d.Cerca, d.Metodo,
            p is null ? null : new PosicionEstimada(p.X, p.Y, p.Radio, p.Nodos));
    }

    /// <summary>Por cada momento, la zona decidida con la misma regla que la última detección.</summary>
    public static async Task<List<(string Zona, DateTime Hora)>> ZonasPorMomento(
        AppDbContext db, int alumnoId, DateTime desde, int maximo, Contexto ctx)
    {
        var lecturas = await Lecturas(db, alumnoId, desde, maximo);
        return Ubicacion.Bloques(lecturas)
            .Select(b => (Ubicacion.Decidir(b, ctx.Zonas, ctx.Calibracion, ctx.AnchoPlano, ctx.AltoPlano).Zona, b.Max(l => l.Hora)))
            .ToList();
    }

    // Lecturas de un alumno desde una hora, en orden, con lo necesario para decidir la zona
    private static async Task<List<Ubicacion.Lectura>> Lecturas(AppDbContext db, int alumnoId, DateTime desde, int maximo = 200)
    {
        var filas = await Proyectar(db.LecturasBle.AsNoTracking()
                .Where(l => l.AlumnoId == alumnoId && l.Timestamp >= desde)
                .OrderBy(l => l.Timestamp)
                .Take(maximo))
            .ToListAsync();
        return filas.Select(ALectura).ToList();
    }

    // Solo las columnas que hacen falta (con la zona y la posición del nodo que la oyó)
    private record FilaLectura(int? AlumnoId, DateTime Timestamp, int ZonaId, string Zona, int Rssi, int UmbralRssi,
        bool Puesta, bool Sos, bool BateriaBaja, int NodoId, double? PlanoX, double? PlanoY);

    private static IQueryable<FilaLectura> Proyectar(IQueryable<LecturaBle> q) =>
        q.Select(l => new FilaLectura(l.AlumnoId, l.Timestamp, l.Nodo.ZonaId, l.Nodo.Zona.Nombre, l.Rssi, l.Nodo.UmbralRssi,
            l.Puesta, l.Sos, l.BateriaBaja, l.NodoId, l.Nodo.PlanoX, l.Nodo.PlanoY));

    private static Ubicacion.Lectura ALectura(FilaLectura f) =>
        new(DateTime.SpecifyKind(f.Timestamp, DateTimeKind.Utc), f.ZonaId, f.Zona, f.Rssi, f.UmbralRssi,
            f.Puesta, f.Sos, f.BateriaBaja, f.NodoId, f.PlanoX, f.PlanoY);
}
