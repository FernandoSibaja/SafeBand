using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Alertas (por ahora SOS). Cada quien ve solo las que le tocan, filtrado aquí en la API:
/// el director todas, el maestro las de sus grupos y el padre las de sus hijos.
/// Desactivar desde la página: solo la escuela (director o maestro titular del grupo del niño).
/// </summary>
public static class AlertasEndpoints
{
    public static void MapAlertas(this WebApplication app)
    {
        var alertas = app.MapGroup("/api/alertas").WithTags("Alertas").RequireAuthorization();

        alertas.MapGet("/", Listar)
            .WithSummary("Alertas que le tocan a quien inició sesión (?activas=true solo las activas; ?desde= historial, por defecto 7 días)");
        alertas.MapPost("/{id:int}/desactivar", Desactivar).RequireAuthorization(Politicas.Personal)
            .WithSummary("Desactiva una alerta activa desde la página (director o maestro titular del grupo)");
    }

    // GET /api/alertas?activas=true
    private static async Task<IResult> Listar(ClaimsPrincipal yo, UserManager<Usuario> usuarios, AppDbContext db,
        bool? activas, DateTimeOffset? desde)
    {
        var (q, miId, esAdmin) = await Visibles(yo, usuarios, db);
        if (activas == true) q = q.Where(a => a.Fin == null);
        else
        {
            var desdeUtc = desde?.UtcDateTime ?? DateTime.UtcNow.AddDays(-7);
            q = q.Where(a => a.Fin == null || a.Inicio >= desdeUtc);
        }

        var lista = await q
            .OrderBy(a => a.Fin != null).ThenByDescending(a => a.Inicio)   // activas primero, luego las más nuevas
            .Take(500)
            .Select(a => new AlertaResponse(
                a.Id, a.Tipo, a.AlumnoId,
                a.Alumno.Nombres + " " + a.Alumno.ApellidoPaterno,
                a.Alumno.Grupo != null ? a.Alumno.Grupo.Nombre : null,
                a.Zona != null ? a.Zona.Nombre : null,
                DateTime.SpecifyKind(a.Inicio, DateTimeKind.Utc),
                a.Fin == null ? null : DateTime.SpecifyKind(a.Fin.Value, DateTimeKind.Utc),
                a.DesactivadaDesde,
                a.DesactivadaPor != null ? a.DesactivadaPor.NombreMostrar : null,
                a.Fin == null && (esAdmin || (a.Alumno.Grupo != null && a.Alumno.Grupo.MaestroId == miId && a.Alumno.Grupo.Activo))))
            .ToListAsync();
        return Results.Ok(lista);
    }

    // POST /api/alertas/5/desactivar
    private static async Task<IResult> Desactivar(int id, ClaimsPrincipal yo, UserManager<Usuario> usuarios, AppDbContext db)
    {
        var alerta = await db.Alertas.Include(a => a.Alumno).ThenInclude(al => al.Grupo).FirstOrDefaultAsync(a => a.Id == id);
        if (alerta is null) return Results.NotFound(new { error = $"No existe la alerta {id}." });

        var miId = usuarios.GetUserId(yo);
        var esAdmin = yo.IsInRole(Roles.Administrador);
        var esSuMaestro = alerta.Alumno.Grupo is { Activo: true } g && g.MaestroId == miId;
        if (!esAdmin && !esSuMaestro)
            return Results.Json(new { error = "Solo el director o el maestro del grupo pueden desactivar esta alerta." },
                statusCode: StatusCodes.Status403Forbidden);
        if (alerta.Fin is not null)
            return Results.Conflict(new { error = "La alerta ya estaba desactivada." });

        alerta.Fin = DateTime.UtcNow;
        alerta.DesactivadaDesde = OrigenDesactivacion.Pagina;
        alerta.DesactivadaPorId = miId;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    /// <summary>Las alertas que le tocan a la cuenta con sesión.</summary>
    private static async Task<(IQueryable<Alerta> Q, string? MiId, bool EsAdmin)> Visibles(
        ClaimsPrincipal yo, UserManager<Usuario> usuarios, AppDbContext db)
    {
        var miId = usuarios.GetUserId(yo);
        var q = db.Alertas.AsNoTracking();
        if (yo.IsInRole(Roles.Administrador)) return (q, miId, true);
        if (yo.IsInRole(Roles.Maestro))
            return (q.Where(a => a.Alumno.Grupo != null && a.Alumno.Grupo.Activo && a.Alumno.Grupo.MaestroId == miId), miId, false);

        // Padre: solo sus hijos
        var tutorId = (await usuarios.GetUserAsync(yo))?.TutorId;
        return (q.Where(a => tutorId != null && a.Alumno.Tutores.Any(t => t.TutorId == tutorId)), miId, false);
    }
}
