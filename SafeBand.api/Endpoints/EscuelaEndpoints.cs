using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;
using SafeBand.api.Servicios;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Panel de la escuela (solo Administrador): todos los alumnos activos y dónde están ahora.
/// La última detección de todos sale de una sola consulta (Detecciones.Ultimas), para que aguante una escuela completa.
/// </summary>
public static class EscuelaEndpoints
{
    public static void MapEscuela(this WebApplication app)
    {
        app.MapGet("/api/escuela", Escuela)
            .RequireAuthorization(Politicas.SoloAdministrador)
            .WithTags("Escuela")
            .WithSummary("Todos los alumnos activos con su grupo y dónde están ahora");
    }

    // GET /api/escuela
    private static async Task<IResult> Escuela(AppDbContext db, IConfiguration config)
    {
        var alumnos = await db.Alumnos.AsNoTracking()
            .Where(a => a.Activo)
            .OrderBy(a => a.ApellidoPaterno).ThenBy(a => a.Nombres)
            .Select(a => new
            {
                a.Id,
                Nombre = a.Nombres + " " + a.ApellidoPaterno,
                a.GrupoId,
                Grupo = a.Grupo != null ? a.Grupo.Nombre : null,
                SalonId = a.Grupo != null ? a.Grupo.ZonaId : null,
                Pulsera = a.Pulseras.Where(p => p.Activa).Select(p => p.IdentificadorBle).FirstOrDefault(),
            })
            .ToListAsync();

        var ctx = await Detecciones.CargarContexto(db, config);
        var ultimas = await Detecciones.Ultimas(db, alumnos.Select(a => a.Id).ToList(), ctx);
        return Results.Ok(alumnos.Select(a => new AlumnoEnEscuela(
            a.Id, a.Nombre, a.GrupoId, a.Grupo, a.SalonId, a.Pulsera, ultimas.GetValueOrDefault(a.Id))).ToList());
    }
}
