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
        grupo.MapPost("/", Crear).WithSummary("Da de alta un tutor");
        grupo.MapPut("/{id:int}", Editar).WithSummary("Edita los datos de un tutor");
        grupo.MapDelete("/{id:int}", DarDeBaja).WithSummary("Da de baja un tutor (no se borra: queda inactivo)");

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
        await db.SaveChangesAsync();

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
        await db.SaveChangesAsync();
        return Results.NoContent();
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
