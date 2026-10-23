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

        var ctx = await Detecciones.CargarContexto(db, config);

        var resultado = new List<MiHijoResponse>();
        foreach (var h in hijos)
        {
            var ultima = await Detecciones.Ultima(db, h.AlumnoId, ctx);
            var recorrido = ArmarRecorrido(await Detecciones.ZonasPorMomento(db, h.AlumnoId, desdeUtc, MaxLecturasRecorrido, ctx));
            resultado.Add(new MiHijoResponse(h.AlumnoId, h.Nombre, h.Parentesco, h.Pulsera, ultima, recorrido));
        }
        return Results.Ok(resultado);
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
