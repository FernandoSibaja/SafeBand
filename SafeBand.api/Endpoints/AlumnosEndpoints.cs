using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Administración de alumnos (solo Administrador). El alumno se guarda junto con su pulsera
/// en una sola operación: o se guarda todo, o nada.
/// </summary>
public static class AlumnosEndpoints
{
    public static void MapAlumnos(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/alumnos").WithTags("Alumnos")
            .RequireAuthorization(Politicas.SoloAdministrador);   // sin sesión → 401; sin rol → 403

        grupo.MapGet("/", Listar).WithSummary("Lista de alumnos (?incluirInactivos=true para ver también los dados de baja)");
        grupo.MapGet("/{id:int}", Obtener).WithSummary("Un alumno por su Id");
        grupo.MapPost("/", Crear).WithSummary("Da de alta un alumno (y opcionalmente le asigna una pulsera libre)");
        grupo.MapPut("/{id:int}", Editar).WithSummary("Edita los datos del alumno y su pulsera (pulseraId null = sin pulsera)");
        grupo.MapDelete("/{id:int}", DarDeBaja).WithSummary("Da de baja un alumno (no se borra: queda inactivo)");
        grupo.MapPost("/{id:int}/reactivar", Reactivar).WithSummary("Vuelve a activar un alumno dado de baja");
    }

    // GET /api/alumnos
    private static async Task<IResult> Listar(AppDbContext db, bool? incluirInactivos)
    {
        var q = db.Alumnos.AsNoTracking();
        if (incluirInactivos != true) q = q.Where(a => a.Activo);

        var alumnos = await Proyectar(q.OrderBy(a => a.ApellidoPaterno).ThenBy(a => a.Nombres)).ToListAsync();
        return Results.Ok(alumnos);
    }

    // GET /api/alumnos/5
    private static async Task<IResult> Obtener(int id, AppDbContext db)
    {
        var alumno = await Proyectar(db.Alumnos.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync();
        return alumno is null
            ? Results.NotFound(new { error = $"No existe el alumno {id}." })
            : Results.Ok(alumno);
    }

    // POST /api/alumnos
    private static async Task<IResult> Crear(GuardarAlumnoRequest req, AppDbContext db)
    {
        var matricula = Limpiar(req.Matricula);
        if (matricula is not null && await db.Alumnos.AnyAsync(a => a.Matricula == matricula))
            return Results.Conflict(new { error = $"Ya existe un alumno con la matrícula '{matricula}'." });

        var (pulsera, error) = await ValidarPulsera(db, req.PulseraId, alumnoId: null);
        if (error is not null) return error;

        var alumno = new Alumno { FechaAlta = DateTime.UtcNow };
        Aplicar(alumno, req, matricula);
        db.Alumnos.Add(alumno);
        if (pulsera is not null) pulsera.Alumno = alumno;   // EF pone el AlumnoId al guardar

        await db.SaveChangesAsync();   // alumno + pulsera en una sola operación

        var respuesta = await Proyectar(db.Alumnos.Where(a => a.Id == alumno.Id)).FirstAsync();
        return Results.Created($"/api/alumnos/{alumno.Id}", respuesta);
    }

    // PUT /api/alumnos/5
    private static async Task<IResult> Editar(int id, GuardarAlumnoRequest req, AppDbContext db)
    {
        var alumno = await db.Alumnos.FindAsync(id);
        if (alumno is null)
            return Results.NotFound(new { error = $"No existe el alumno {id}." });

        var matricula = Limpiar(req.Matricula);
        if (matricula is not null && await db.Alumnos.AnyAsync(a => a.Matricula == matricula && a.Id != id))
            return Results.Conflict(new { error = $"Ya existe otro alumno con la matrícula '{matricula}'." });

        if (!alumno.Activo && req.PulseraId is not null)
            return Results.BadRequest(new { error = "No se puede asignar una pulsera a un alumno dado de baja. Reactívalo primero." });

        var (nueva, error) = await ValidarPulsera(db, req.PulseraId, alumnoId: id);
        if (error is not null) return error;

        Aplicar(alumno, req, matricula);

        // La pulsera que tenía y ya no es la elegida queda libre
        var actuales = await db.Pulseras.Where(p => p.AlumnoId == id && p.Activa).ToListAsync();
        foreach (var p in actuales.Where(p => p.Id != req.PulseraId))
            p.AlumnoId = null;
        if (nueva is not null) nueva.AlumnoId = id;

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // DELETE /api/alumnos/5  → baja lógica: no se borra para conservar su historial
    private static async Task<IResult> DarDeBaja(int id, AppDbContext db)
    {
        var alumno = await db.Alumnos.FindAsync(id);
        if (alumno is null)
            return Results.NotFound(new { error = $"No existe el alumno {id}." });

        alumno.Activo = false;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // POST /api/alumnos/5/reactivar
    private static async Task<IResult> Reactivar(int id, AppDbContext db)
    {
        var alumno = await db.Alumnos.FindAsync(id);
        if (alumno is null)
            return Results.NotFound(new { error = $"No existe el alumno {id}." });

        alumno.Activo = true;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ---------- Ayudantes ----------

    /// <summary>
    /// Revisa que la pulsera elegida se pueda asignar: que exista, esté activa
    /// y no pertenezca a otro alumno (para no quitarle la pulsera a otro niño por error).
    /// </summary>
    private static async Task<(Pulsera? pulsera, IResult? error)> ValidarPulsera(AppDbContext db, int? pulseraId, int? alumnoId)
    {
        if (pulseraId is null) return (null, null);

        var p = await db.Pulseras.Include(x => x.Alumno).FirstOrDefaultAsync(x => x.Id == pulseraId);
        if (p is null)
            return (null, Results.BadRequest(new { error = $"La pulsera {pulseraId} no existe." }));
        if (!p.Activa)
            return (null, Results.BadRequest(new { error = $"La pulsera {p.IdentificadorBle} está dada de baja." }));
        if (p.AlumnoId is not null && p.AlumnoId != alumnoId)
            return (null, Results.Conflict(new
            {
                error = $"La pulsera {p.IdentificadorBle} ya es de {p.Alumno!.Nombres} {p.Alumno.ApellidoPaterno}. Quítasela primero."
            }));

        return (p, null);
    }

    private static void Aplicar(Alumno alumno, GuardarAlumnoRequest req, string? matricula)
    {
        alumno.Nombres = req.Nombres!.Trim();
        alumno.ApellidoPaterno = req.ApellidoPaterno!.Trim();
        alumno.ApellidoMaterno = Limpiar(req.ApellidoMaterno);
        alumno.FechaNacimiento = req.FechaNacimiento;
        alumno.Matricula = matricula;
    }

    // Quita espacios; si queda vacío, lo guarda como null
    private static string? Limpiar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static IQueryable<AlumnoResponse> Proyectar(IQueryable<Alumno> q) =>
        q.Select(a => new AlumnoResponse(
            a.Id, a.Nombres, a.ApellidoPaterno, a.ApellidoMaterno, a.FechaNacimiento,
            a.Matricula, a.Activo,
            DateTime.SpecifyKind(a.FechaAlta, DateTimeKind.Utc),
            a.Pulseras.Where(p => p.Activa)
                .Select(p => new PulseraResumen(p.Id, p.IdentificadorBle))
                .FirstOrDefault()));
}
