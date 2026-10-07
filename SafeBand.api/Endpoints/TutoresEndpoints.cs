using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Administración de tutores y de su vínculo con los alumnos (qué niños ve cada padre).
/// Por ahora sin inicio de sesión; se protegerá en el paso de usuarios y roles.
/// </summary>
public static class TutoresEndpoints
{
    public static void MapTutores(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/tutores").WithTags("Tutores")
            .RequireAuthorization(Politicas.SoloAdministrador);

        grupo.MapGet("/", Listar).WithSummary("Lista de tutores con sus hijos (?incluirInactivos=true)");
        grupo.MapGet("/{id:int}", Obtener).WithSummary("Un tutor con sus hijos");
        grupo.MapPost("/", Crear).WithSummary("Da de alta un tutor (y sus hijos, si se envían)");
        grupo.MapPut("/{id:int}", Editar).WithSummary("Edita los datos de un tutor (y sus hijos, si se envían)");
        grupo.MapDelete("/{id:int}", DarDeBaja).WithSummary("Da de baja un tutor (no se borra: queda inactivo)");
        grupo.MapPost("/{id:int}/reactivar", Reactivar).WithSummary("Vuelve a activar un tutor dado de baja");

        grupo.MapPut("/{id:int}/alumnos/{alumnoId:int}", Vincular)
            .WithSummary("Vincula al tutor con un alumno (o actualiza parentesco, contacto principal y si puede recoger)");
        grupo.MapDelete("/{id:int}/alumnos/{alumnoId:int}", Desvincular)
            .WithSummary("Quita el vínculo: el tutor deja de ver a ese alumno");
    }

    // GET /api/tutores
    private static async Task<IResult> Listar(AppDbContext db, bool? incluirInactivos)
    {
        var q = db.Tutores.AsNoTracking();
        if (incluirInactivos != true) q = q.Where(t => t.Activo);

        return Results.Ok(await Proyectar(q.OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.Nombres)).ToListAsync());
    }

    // GET /api/tutores/5
    private static async Task<IResult> Obtener(int id, AppDbContext db)
    {
        var tutor = await Proyectar(db.Tutores.AsNoTracking().Where(t => t.Id == id)).FirstOrDefaultAsync();
        return tutor is null
            ? Results.NotFound(new { error = $"No existe el tutor {id}." })
            : Results.Ok(tutor);
    }

    // POST /api/tutores
    private static async Task<IResult> Crear(GuardarTutorRequest req, AppDbContext db)
    {
        var email = req.Email!.Trim().ToLowerInvariant();
        if (await db.Tutores.AnyAsync(t => t.Email == email))
            return Results.Conflict(new { error = $"Ya existe un tutor con el correo {email}." });

        var tutor = new Tutor { FechaAlta = DateTime.UtcNow };
        Aplicar(tutor, req, email);
        db.Tutores.Add(tutor);

        var error = await AplicarHijos(db, tutor, req.Hijos);
        if (error is not null) return error;

        await db.SaveChangesAsync();   // tutor + vínculos con sus hijos en una sola operación

        var respuesta = await Proyectar(db.Tutores.Where(t => t.Id == tutor.Id)).FirstAsync();
        return Results.Created($"/api/tutores/{tutor.Id}", respuesta);
    }

    // PUT /api/tutores/5
    private static async Task<IResult> Editar(int id, GuardarTutorRequest req, AppDbContext db)
    {
        var tutor = await db.Tutores.FindAsync(id);
        if (tutor is null) return Results.NotFound(new { error = $"No existe el tutor {id}." });

        var email = req.Email!.Trim().ToLowerInvariant();
        if (await db.Tutores.AnyAsync(t => t.Email == email && t.Id != id))
            return Results.Conflict(new { error = $"Ya existe otro tutor con el correo {email}." });

        Aplicar(tutor, req, email);

        var error = await AplicarHijos(db, tutor, req.Hijos);
        if (error is not null) return error;

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // POST /api/tutores/5/reactivar
    private static async Task<IResult> Reactivar(int id, AppDbContext db)
    {
        var tutor = await db.Tutores.FindAsync(id);
        if (tutor is null) return Results.NotFound(new { error = $"No existe el tutor {id}." });

        tutor.Activo = true;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    /// <summary>
    /// Deja los hijos del tutor exactamente como en la lista recibida (agrega, actualiza y quita vínculos).
    /// Los vínculos con alumnos dados de baja no se tocan: no se ven en la pantalla y se conservan
    /// por si el alumno se reactiva. Devuelve un error, o null si todo está bien.
    /// </summary>
    private static async Task<IResult?> AplicarHijos(AppDbContext db, Tutor tutor, List<HijoRequest>? hijos)
    {
        if (hijos is null) return null;   // no se enviaron: no se cambian los vínculos

        var ids = hijos.Select(h => h.AlumnoId).ToList();
        if (ids.Count != ids.Distinct().Count())
            return Results.BadRequest(new { error = "Un alumno aparece dos veces en la lista de hijos." });

        var alumnos = await db.Alumnos.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id);
        foreach (var id in ids)
        {
            if (!alumnos.TryGetValue(id, out var a))
                return Results.BadRequest(new { error = $"El alumno {id} no existe." });
            if (!a.Activo)
                return Results.BadRequest(new { error = $"{a.Nombres} {a.ApellidoPaterno} está dado de baja; reactívalo para vincularlo." });
        }

        var actuales = tutor.Id == 0
            ? []
            : await db.TutoresAlumnos.Include(ta => ta.Alumno).Where(ta => ta.TutorId == tutor.Id).ToListAsync();

        // Quitar los hijos (activos) que ya no vienen en la lista
        foreach (var ta in actuales.Where(ta => ta.Alumno.Activo && !ids.Contains(ta.AlumnoId)))
            db.TutoresAlumnos.Remove(ta);

        foreach (var h in hijos)
        {
            var vinculo = actuales.FirstOrDefault(ta => ta.AlumnoId == h.AlumnoId);
            if (vinculo is null)
            {
                vinculo = new TutorAlumno { Tutor = tutor, AlumnoId = h.AlumnoId };
                db.TutoresAlumnos.Add(vinculo);
            }
            vinculo.Parentesco = string.IsNullOrWhiteSpace(h.Parentesco) ? null : h.Parentesco.Trim();
            vinculo.EsContactoPrincipal = h.EsContactoPrincipal;
            vinculo.PuedeRecoger = h.PuedeRecoger;

            // Solo un contacto principal por alumno: los otros tutores de ese niño dejan de serlo
            if (h.EsContactoPrincipal)
            {
                var otros = await db.TutoresAlumnos
                    .Where(ta => ta.AlumnoId == h.AlumnoId && ta.TutorId != tutor.Id && ta.EsContactoPrincipal)
                    .ToListAsync();
                foreach (var o in otros) o.EsContactoPrincipal = false;
            }
        }
        return null;
    }

    // DELETE /api/tutores/5  → baja lógica
    private static async Task<IResult> DarDeBaja(int id, AppDbContext db)
    {
        var tutor = await db.Tutores.FindAsync(id);
        if (tutor is null) return Results.NotFound(new { error = $"No existe el tutor {id}." });

        tutor.Activo = false;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // PUT /api/tutores/5/alumnos/3   { "parentesco": "Madre", "esContactoPrincipal": true, "puedeRecoger": true }
    private static async Task<IResult> Vincular(int id, int alumnoId, VincularAlumnoRequest req, AppDbContext db)
    {
        var tutor = await db.Tutores.FindAsync(id);
        if (tutor is null) return Results.NotFound(new { error = $"No existe el tutor {id}." });
        if (!tutor.Activo) return Results.BadRequest(new { error = "El tutor está dado de baja." });

        var alumno = await db.Alumnos.FindAsync(alumnoId);
        if (alumno is null) return Results.NotFound(new { error = $"No existe el alumno {alumnoId}." });
        if (!alumno.Activo) return Results.BadRequest(new { error = "El alumno está dado de baja." });

        // Si el vínculo ya existe se actualiza; si no, se crea
        var vinculo = await db.TutoresAlumnos.FindAsync(id, alumnoId);
        if (vinculo is null)
        {
            vinculo = new TutorAlumno { TutorId = id, AlumnoId = alumnoId };
            db.TutoresAlumnos.Add(vinculo);
        }
        vinculo.Parentesco = string.IsNullOrWhiteSpace(req.Parentesco) ? null : req.Parentesco.Trim();
        vinculo.PuedeRecoger = req.PuedeRecoger;
        vinculo.EsContactoPrincipal = req.EsContactoPrincipal;

        // Solo un contacto principal por alumno: si este lo es, los demás dejan de serlo
        if (req.EsContactoPrincipal)
        {
            var otros = await db.TutoresAlumnos
                .Where(ta => ta.AlumnoId == alumnoId && ta.TutorId != id && ta.EsContactoPrincipal)
                .ToListAsync();
            foreach (var o in otros) o.EsContactoPrincipal = false;
        }

        await db.SaveChangesAsync();
        return Results.Ok(await Proyectar(db.Tutores.Where(t => t.Id == id)).FirstAsync());
    }

    // DELETE /api/tutores/5/alumnos/3
    private static async Task<IResult> Desvincular(int id, int alumnoId, AppDbContext db)
    {
        var vinculo = await db.TutoresAlumnos.FindAsync(id, alumnoId);
        if (vinculo is null)
            return Results.NotFound(new { error = $"El tutor {id} no está vinculado con el alumno {alumnoId}." });

        db.TutoresAlumnos.Remove(vinculo);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ---------- Ayudantes ----------

    private static void Aplicar(Tutor tutor, GuardarTutorRequest req, string email)
    {
        tutor.Nombres = req.Nombres!.Trim();
        tutor.ApellidoPaterno = req.ApellidoPaterno!.Trim();
        tutor.ApellidoMaterno = string.IsNullOrWhiteSpace(req.ApellidoMaterno) ? null : req.ApellidoMaterno.Trim();
        tutor.Email = email;
        tutor.Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
    }

    private static IQueryable<TutorResponse> Proyectar(IQueryable<Tutor> q) =>
        q.Select(t => new TutorResponse(
            t.Id, t.Nombres, t.ApellidoPaterno, t.ApellidoMaterno, t.Email, t.Telefono, t.Activo,
            DateTime.SpecifyKind(t.FechaAlta, DateTimeKind.Utc),
            t.Alumnos
                .Where(ta => ta.Alumno.Activo)
                .Select(ta => new HijoResponse(
                    ta.AlumnoId,
                    ta.Alumno.Nombres + " " + ta.Alumno.ApellidoPaterno,
                    ta.Parentesco, ta.EsContactoPrincipal, ta.PuedeRecoger))
                .ToList()));
}
