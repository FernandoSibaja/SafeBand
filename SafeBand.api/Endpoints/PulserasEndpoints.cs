using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Administración de pulseras: alta, edición, asignación a un alumno y baja.
/// Por ahora sin inicio de sesión; se protegerá en el paso de usuarios y roles.
/// </summary>
public static class PulserasEndpoints
{
    public static void MapPulseras(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/pulseras").WithTags("Pulseras")
            .RequireAuthorization(Politicas.SoloAdministrador);

        grupo.MapGet("/", Listar).WithSummary("Lista de pulseras (?sinAsignar=true solo las libres; ?incluirInactivas=true)");
        grupo.MapGet("/{id:int}", Obtener).WithSummary("Una pulsera por su Id");
        grupo.MapPost("/", Crear).WithSummary("Da de alta una pulsera (sin asignar)");
        grupo.MapPut("/{id:int}", Editar).WithSummary("Edita el identificador BLE o el UID NFC");
        grupo.MapPut("/{id:int}/alumno", Asignar).WithSummary("Asigna la pulsera a un alumno (alumnoId = null para quitarla)");
        grupo.MapDelete("/{id:int}", DarDeBaja).WithSummary("Da de baja la pulsera (perdida o dañada); no se borra");
    }

    // GET /api/pulseras
    private static async Task<IResult> Listar(AppDbContext db, bool? sinAsignar, bool? incluirInactivas)
    {
        var q = db.Pulseras.AsNoTracking();
        if (incluirInactivas != true) q = q.Where(p => p.Activa);
        if (sinAsignar == true) q = q.Where(p => p.AlumnoId == null);

        return Results.Ok(await Proyectar(q.OrderBy(p => p.IdentificadorBle)).ToListAsync());
    }

    // GET /api/pulseras/5
    private static async Task<IResult> Obtener(int id, AppDbContext db)
    {
        var pulsera = await Proyectar(db.Pulseras.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync();
        return pulsera is null
            ? Results.NotFound(new { error = $"No existe la pulsera {id}." })
            : Results.Ok(pulsera);
    }

    // POST /api/pulseras
    private static async Task<IResult> Crear(GuardarPulseraRequest req, AppDbContext db)
    {
        var idBle = req.IdentificadorBle!.Trim().ToUpperInvariant();
        var (uid, errorUid) = NormalizarUid(req.UidNfc);
        if (errorUid is not null) return Results.BadRequest(new { error = errorUid });

        var conflicto = await BuscarDuplicado(db, idBle, uid, idActual: null);
        if (conflicto is not null) return Results.Conflict(new { error = conflicto });

        var pulsera = new Pulsera { IdentificadorBle = idBle, UidNfc = uid, FechaAlta = DateTime.UtcNow };
        db.Pulseras.Add(pulsera);
        await db.SaveChangesAsync();

        var respuesta = await Proyectar(db.Pulseras.Where(p => p.Id == pulsera.Id)).FirstAsync();
        return Results.Created($"/api/pulseras/{pulsera.Id}", respuesta);
    }

    // PUT /api/pulseras/5
    private static async Task<IResult> Editar(int id, GuardarPulseraRequest req, AppDbContext db)
    {
        var pulsera = await db.Pulseras.FindAsync(id);
        if (pulsera is null) return Results.NotFound(new { error = $"No existe la pulsera {id}." });

        var idBle = req.IdentificadorBle!.Trim().ToUpperInvariant();
        var (uid, errorUid) = NormalizarUid(req.UidNfc);
        if (errorUid is not null) return Results.BadRequest(new { error = errorUid });

        var conflicto = await BuscarDuplicado(db, idBle, uid, idActual: id);
        if (conflicto is not null) return Results.Conflict(new { error = conflicto });

        pulsera.IdentificadorBle = idBle;
        pulsera.UidNfc = uid;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // PUT /api/pulseras/5/alumno   { "alumnoId": 3 }  o  { "alumnoId": null }
    private static async Task<IResult> Asignar(int id, AsignarPulseraRequest req, AppDbContext db)
    {
        var pulsera = await db.Pulseras.Include(p => p.Alumno).FirstOrDefaultAsync(p => p.Id == id);
        if (pulsera is null) return Results.NotFound(new { error = $"No existe la pulsera {id}." });

        // Quitar la asignación
        if (req.AlumnoId is null)
        {
            pulsera.AlumnoId = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }

        if (!pulsera.Activa)
            return Results.BadRequest(new { error = $"La pulsera {pulsera.IdentificadorBle} está dada de baja." });

        var alumno = await db.Alumnos.FindAsync(req.AlumnoId.Value);
        if (alumno is null || !alumno.Activo)
            return Results.BadRequest(new { error = $"El alumno {req.AlumnoId} no existe o está dado de baja." });

        if (pulsera.AlumnoId == alumno.Id) return Results.NoContent();   // ya estaba asignada a él

        // Regla 1: no quitarle la pulsera a otro niño por accidente
        if (pulsera.AlumnoId is not null)
            return Results.Conflict(new
            {
                error = $"La pulsera {pulsera.IdentificadorBle} ya está asignada a {pulsera.Alumno!.Nombres} {pulsera.Alumno.ApellidoPaterno}. Quítale la asignación primero."
            });

        // Regla 2: un alumno solo puede tener una pulsera activa
        var otra = await db.Pulseras.FirstOrDefaultAsync(p => p.AlumnoId == alumno.Id && p.Activa && p.Id != id);
        if (otra is not null)
            return Results.Conflict(new
            {
                error = $"{alumno.Nombres} {alumno.ApellidoPaterno} ya tiene la pulsera {otra.IdentificadorBle} activa. Dala de baja o quítale la asignación primero."
            });

        pulsera.AlumnoId = alumno.Id;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // DELETE /api/pulseras/5  → baja lógica (pulsera perdida o dañada); conserva sus lecturas
    private static async Task<IResult> DarDeBaja(int id, AppDbContext db)
    {
        var pulsera = await db.Pulseras.FindAsync(id);
        if (pulsera is null) return Results.NotFound(new { error = $"No existe la pulsera {id}." });

        pulsera.Activa = false;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ---------- Ayudantes ----------

    // Quita separadores (":", "-", espacios) y valida que sea hexadecimal
    private static (string? uid, string? error) NormalizarUid(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null);
        var limpio = Regex.Replace(texto, @"[\s:\-]", "").ToUpperInvariant();
        if (!Regex.IsMatch(limpio, "^[0-9A-F]{8,20}$"))
            return (null, "El campo 'uidNfc' debe ser hexadecimal (ej. 04A1B2C3D4E5F6).");
        return (limpio, null);
    }

    private static async Task<string?> BuscarDuplicado(AppDbContext db, string idBle, string? uid, int? idActual)
    {
        if (await db.Pulseras.AnyAsync(p => p.IdentificadorBle == idBle && p.Id != idActual))
            return $"Ya existe una pulsera con el identificador {idBle}.";
        if (uid is not null && await db.Pulseras.AnyAsync(p => p.UidNfc == uid && p.Id != idActual))
            return $"Ya existe una pulsera con el UID NFC {uid}.";
        return null;
    }

    private static IQueryable<PulseraResponse> Proyectar(IQueryable<Pulsera> q) =>
        q.Select(p => new PulseraResponse(
            p.Id, p.IdentificadorBle, p.UidNfc, p.Activa,
            DateTime.SpecifyKind(p.FechaAlta, DateTimeKind.Utc),
            p.UltimaLectura == null ? null : DateTime.SpecifyKind(p.UltimaLectura.Value, DateTimeKind.Utc),
            p.AlumnoId,
            p.Alumno == null ? null : p.Alumno.Nombres + " " + p.Alumno.ApellidoPaterno));
}
