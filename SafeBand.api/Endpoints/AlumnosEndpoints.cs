using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Administración de alumnos (lo usará el administrador de la escuela).
/// Por ahora sin inicio de sesión; se protegerá en el paso de usuarios y roles.
/// </summary>
public static class AlumnosEndpoints
{
    public static void MapAlumnos(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/alumnos").WithTags("Alumnos")
            .RequireAuthorization(Politicas.SoloAdministrador);   // sin sesión → 401; sin rol → 403

        grupo.MapGet("/", Listar).WithSummary("Lista de alumnos (?incluirInactivos=true para ver también los dados de baja)");
        grupo.MapGet("/{id:int}", Obtener).WithSummary("Un alumno por su Id");
        grupo.MapPost("/", Crear).WithSummary("Da de alta un alumno");
        grupo.MapPut("/{id:int}", Editar).WithSummary("Edita los datos de un alumno");
        grupo.MapDelete("/{id:int}", DarDeBaja).WithSummary("Da de baja un alumno (no se borra: queda inactivo)");
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

        var alumno = new Alumno { FechaAlta = DateTime.UtcNow };
        Aplicar(alumno, req, matricula);
        db.Alumnos.Add(alumno);
        await db.SaveChangesAsync();

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

        Aplicar(alumno, req, matricula);
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

    // ---------- Ayudantes ----------

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
            a.Pulseras.Where(p => p.Activa).Select(p => p.IdentificadorBle).ToList()));
}
