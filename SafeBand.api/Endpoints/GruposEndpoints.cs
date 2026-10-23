using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Administración de grupos escolares (solo Administrador): nombre, salón, maestro titular y sus alumnos.
/// El grupo y la lista de alumnos se guardan en una sola operación.
/// </summary>
public static class GruposEndpoints
{
    public static void MapGrupos(this WebApplication app)
    {
        var grupos = app.MapGroup("/api/grupos").WithTags("Grupos")
            .RequireAuthorization(Politicas.SoloAdministrador);

        grupos.MapGet("/", Listar).WithSummary("Lista de grupos con salón, maestro y alumnos (?incluirInactivos=true)");
        grupos.MapGet("/{id:int}", Obtener);
        grupos.MapPost("/", Crear).WithSummary("Da de alta un grupo (y opcionalmente le asigna alumnos)");
        grupos.MapPut("/{id:int}", Editar).WithSummary("Edita un grupo; alumnoIds = los alumnos que deben quedar (null = no tocar)");
        grupos.MapDelete("/{id:int}", DarDeBaja).WithSummary("Da de baja un grupo (solo si ya no tiene alumnos activos)");
        grupos.MapPost("/{id:int}/reactivar", Reactivar).WithSummary("Vuelve a activar un grupo dado de baja");
        grupos.MapGet("/maestros", Maestros).WithSummary("Cuentas activas con rol Maestro (para elegir al titular)");
    }

    // GET /api/grupos
    private static async Task<IResult> Listar(AppDbContext db, bool? incluirInactivos)
    {
        var q = db.Grupos.AsNoTracking();
        if (incluirInactivos != true) q = q.Where(g => g.Activo);
        return Results.Ok(await Proyectar(q.OrderBy(g => g.Nombre)).ToListAsync());
    }

    // GET /api/grupos/5
    private static async Task<IResult> Obtener(int id, AppDbContext db)
    {
        var grupo = await Proyectar(db.Grupos.AsNoTracking().Where(g => g.Id == id)).FirstOrDefaultAsync();
        return grupo is null ? Results.NotFound(new { error = $"No existe el grupo {id}." }) : Results.Ok(grupo);
    }

    // POST /api/grupos
    private static async Task<IResult> Crear(GuardarGrupoRequest req, AppDbContext db, UserManager<Usuario> usuarios)
    {
        var nombre = req.Nombre!.Trim();
        if (await db.Grupos.AnyAsync(g => g.Nombre == nombre))
            return Results.Conflict(new { error = $"Ya existe un grupo llamado '{nombre}'." });

        var error = await Validar(req, db, usuarios);
        if (error is not null) return error;

        var grupo = new Grupo { Nombre = nombre, ZonaId = req.ZonaId, MaestroId = Limpiar(req.MaestroId) };
        db.Grupos.Add(grupo);
        await db.SaveChangesAsync();   // primero el grupo, para tener su Id
        if (req.AlumnoIds is not null) await AsignarAlumnos(db, grupo.Id, req.AlumnoIds);
        await db.SaveChangesAsync();

        var respuesta = await Proyectar(db.Grupos.Where(g => g.Id == grupo.Id)).FirstAsync();
        return Results.Created($"/api/grupos/{grupo.Id}", respuesta);
    }

    // PUT /api/grupos/5
    private static async Task<IResult> Editar(int id, GuardarGrupoRequest req, AppDbContext db, UserManager<Usuario> usuarios)
    {
        var grupo = await db.Grupos.FindAsync(id);
        if (grupo is null) return Results.NotFound(new { error = $"No existe el grupo {id}." });

        var nombre = req.Nombre!.Trim();
        if (await db.Grupos.AnyAsync(g => g.Nombre == nombre && g.Id != id))
            return Results.Conflict(new { error = $"Ya existe otro grupo llamado '{nombre}'." });
        if (!grupo.Activo && req.AlumnoIds is { Count: > 0 })
            return Results.BadRequest(new { error = "No se pueden asignar alumnos a un grupo dado de baja. Reactívalo primero." });

        var error = await Validar(req, db, usuarios);
        if (error is not null) return error;

        grupo.Nombre = nombre;
        grupo.ZonaId = req.ZonaId;
        grupo.MaestroId = Limpiar(req.MaestroId);
        if (req.AlumnoIds is not null) await AsignarAlumnos(db, id, req.AlumnoIds);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // DELETE /api/grupos/5 → baja lógica
    private static async Task<IResult> DarDeBaja(int id, AppDbContext db)
    {
        var grupo = await db.Grupos.FindAsync(id);
        if (grupo is null) return Results.NotFound(new { error = $"No existe el grupo {id}." });

        // Con alumnos adentro, darlo de baja los dejaría en un grupo que ya no existe
        if (await db.Alumnos.AnyAsync(a => a.GrupoId == id && a.Activo))
            return Results.Conflict(new { error = $"El grupo '{grupo.Nombre}' tiene alumnos activos. Cámbialos de grupo o quítalos primero." });

        grupo.Activo = false;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // POST /api/grupos/5/reactivar
    private static async Task<IResult> Reactivar(int id, AppDbContext db)
    {
        var grupo = await db.Grupos.FindAsync(id);
        if (grupo is null) return Results.NotFound(new { error = $"No existe el grupo {id}." });

        grupo.Activo = true;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // GET /api/grupos/maestros
    private static async Task<IResult> Maestros(UserManager<Usuario> usuarios)
    {
        var maestros = (await usuarios.GetUsersInRoleAsync(Roles.Maestro))
            .Where(u => u.Activo)
            .OrderBy(u => u.NombreMostrar)
            .Select(u => new MaestroResumen(u.Id, u.NombreMostrar, u.Email))
            .ToList();
        return Results.Ok(maestros);
    }

    // ---------- Ayudantes ----------

    /// <summary>Que el salón, el maestro y los alumnos existan y estén activos.</summary>
    private static async Task<IResult?> Validar(GuardarGrupoRequest req, AppDbContext db, UserManager<Usuario> usuarios)
    {
        if (req.ZonaId is not null && !await db.Zonas.AnyAsync(z => z.Id == req.ZonaId && z.Activa))
            return Results.BadRequest(new { error = $"La zona {req.ZonaId} no existe o está dada de baja." });

        var maestroId = Limpiar(req.MaestroId);
        if (maestroId is not null)
        {
            var maestro = await usuarios.FindByIdAsync(maestroId);
            if (maestro is null || !maestro.Activo || !await usuarios.IsInRoleAsync(maestro, Roles.Maestro))
                return Results.BadRequest(new { error = "El maestro elegido no existe, está dado de baja o no tiene el rol Maestro." });
        }

        if (req.AlumnoIds is { Count: > 0 })
        {
            var ids = req.AlumnoIds.Distinct().ToList();
            var validos = await db.Alumnos.CountAsync(a => ids.Contains(a.Id) && a.Activo);
            if (validos != ids.Count)
                return Results.BadRequest(new { error = "Algún alumno de la lista no existe o está dado de baja." });
        }
        return null;
    }

    /// <summary>Deja en el grupo exactamente estos alumnos: los nuevos entran (aunque vengan de otro grupo), los que faltan salen.</summary>
    private static async Task AsignarAlumnos(AppDbContext db, int grupoId, List<int> alumnoIds)
    {
        var ids = alumnoIds.Distinct().ToList();
        var afectados = await db.Alumnos.Where(a => a.GrupoId == grupoId || ids.Contains(a.Id)).ToListAsync();
        foreach (var a in afectados)
            a.GrupoId = ids.Contains(a.Id) ? grupoId : null;
    }

    private static string? Limpiar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static IQueryable<GrupoResponse> Proyectar(IQueryable<Grupo> q) =>
        q.Select(g => new GrupoResponse(
            g.Id, g.Nombre, g.Activo,
            g.ZonaId, g.Zona != null ? g.Zona.Nombre : null,
            g.MaestroId, g.Maestro != null ? g.Maestro.NombreMostrar : null,
            g.Alumnos.Where(a => a.Activo)
                .OrderBy(a => a.ApellidoPaterno).ThenBy(a => a.Nombres)
                .Select(a => new AlumnoDeGrupo(a.Id, a.Nombres + " " + a.ApellidoPaterno))
                .ToList()));
}
