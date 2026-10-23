using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;
using SafeBand.api.Servicios;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Lo que ve un maestro: SOLO los grupos donde es maestro titular y sus alumnos.
/// El filtro se hace aquí, en la API: aunque alguien llame la API a mano, no recibe alumnos de otros grupos.
/// </summary>
public static class MiGrupoEndpoints
{
    public static void MapMiGrupo(this WebApplication app)
    {
        app.MapGet("/api/mi-grupo", MiGrupo)
            .RequireAuthorization(Politicas.SoloMaestro)
            .WithTags("Escuela")
            .WithSummary("Los grupos del maestro con sesión y dónde está cada alumno ahora");
    }

    // GET /api/mi-grupo
    private static async Task<IResult> MiGrupo(ClaimsPrincipal yo, UserManager<Usuario> usuarios, AppDbContext db, IConfiguration config)
    {
        var miId = usuarios.GetUserId(yo);
        var grupos = await db.Grupos.AsNoTracking()
            .Where(g => g.MaestroId == miId && g.Activo)
            .OrderBy(g => g.Nombre)
            .Select(g => new
            {
                g.Id, g.Nombre, g.ZonaId, Zona = g.Zona != null ? g.Zona.Nombre : null,
                Alumnos = g.Alumnos.Where(a => a.Activo)
                    .OrderBy(a => a.ApellidoPaterno).ThenBy(a => a.Nombres)
                    .Select(a => new
                    {
                        a.Id,
                        Nombre = a.Nombres + " " + a.ApellidoPaterno,
                        Pulsera = a.Pulseras.Where(p => p.Activa).Select(p => p.IdentificadorBle).FirstOrDefault(),
                    })
                    .ToList(),
            })
            .ToListAsync();
        if (grupos.Count == 0) return Results.Ok(new List<MiGrupoResponse>());

        var ctx = await Detecciones.CargarContexto(db, config);
        var resultado = new List<MiGrupoResponse>();
        foreach (var g in grupos)
        {
            var ultimas = await Detecciones.Ultimas(db, g.Alumnos.Select(a => a.Id).ToList(), ctx);
            var alumnos = g.Alumnos
                .Select(a => new AlumnoDeMiGrupo(a.Id, a.Nombre, a.Pulsera, ultimas.GetValueOrDefault(a.Id)))
                .ToList();
            resultado.Add(new MiGrupoResponse(g.Id, g.Nombre, g.ZonaId, g.Zona, alumnos));
        }
        return Results.Ok(resultado);
    }
}
