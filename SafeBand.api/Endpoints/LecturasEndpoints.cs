using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>Endpoints de lecturas BLE: el ESP32 las envía (POST) y el frontend las consulta (GET).</summary>
public static class LecturasEndpoints
{
    public static void MapLecturas(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/lecturas").WithTags("Lecturas BLE");

        // TEMPORALMENTE ABIERTOS (sin sesión):
        //   POST: lo usan los ESP32, que no inician sesión → se protegerá con una clave por nodo (paso 6).
        //   GET:  lo usa la página de lecturas, que aún no tiene login → se protegerá en el paso 5
        //         (y ahí el padre verá solo las lecturas de sus hijos).
        grupo.MapPost("/", Crear).AllowAnonymous().WithSummary("Guarda una lectura enviada por un nodo ESP32");
        grupo.MapGet("/", Listar).AllowAnonymous().WithSummary("Últimas lecturas (?limit=1 = la más reciente)");
    }

    // POST /api/lecturas
    private static async Task<IResult> Crear(CrearLecturaRequest req, AppDbContext db)
    {
        // 1. Buscar el nodo y la pulsera por su código (deben existir)
        var nodo = await db.Nodos.Include(n => n.Zona).FirstOrDefaultAsync(n => n.Codigo == req.Nodo);
        if (nodo is null || !nodo.Activo)
            return Results.BadRequest(new { error = $"El nodo '{req.Nodo}' no está registrado o está inactivo." });

        var pulsera = await db.Pulseras.Include(p => p.Alumno).FirstOrDefaultAsync(p => p.IdentificadorBle == req.Pulsera);
        if (pulsera is null || !pulsera.Activa)
            return Results.BadRequest(new { error = $"La pulsera '{req.Pulsera}' no está registrada o está inactiva." });

        // 2. Hora: la que manda el ESP32 (convertida a UTC) o la actual
        var ahora = DateTime.UtcNow;
        var hora = req.Timestamp?.UtcDateTime ?? ahora;
        if (hora > ahora.AddMinutes(5))
            return Results.BadRequest(new { error = "El campo 'timestamp' no puede estar en el futuro." });

        // 3. Guardar la lectura
        var lectura = new LecturaBle
        {
            NodoId = nodo.Id,
            PulseraId = pulsera.Id,
            Rssi = req.Rssi!.Value,
            Puesta = req.Puesta,
            Sos = req.Sos,
            BateriaBaja = req.BateriaBaja,
            Timestamp = hora
        };
        db.LecturasBle.Add(lectura);

        // De paso, anotar cuándo se vio por última vez al nodo y a la pulsera
        nodo.UltimoContacto = ahora;
        pulsera.UltimaLectura = hora;

        await db.SaveChangesAsync();

        // 4. Responder 201 Created con la lectura guardada
        var respuesta = new LecturaResponse(
            lectura.Id, nodo.Codigo, nodo.Zona.Nombre, pulsera.IdentificadorBle,
            NombreCompleto(pulsera.Alumno), lectura.Rssi, "dBm",
            lectura.Puesta, lectura.Sos, lectura.BateriaBaja, lectura.Timestamp);

        return Results.Created($"/api/lecturas/{lectura.Id}", respuesta);
    }

    // GET /api/lecturas?limit=N
    private static async Task<IResult> Listar(AppDbContext db, int? limit)
    {
        var cantidad = limit ?? 100;
        if (cantidad < 1 || cantidad > 1000)
            return Results.BadRequest(new { error = "El parámetro 'limit' debe estar entre 1 y 1000." });

        var lecturas = await db.LecturasBle
            .AsNoTracking()                          // solo lectura: más rápido
            .OrderByDescending(l => l.Timestamp)     // más recientes primero
            .Take(cantidad)
            .Select(l => new LecturaResponse(
                l.Id, l.Nodo.Codigo, l.Nodo.Zona.Nombre, l.Pulsera.IdentificadorBle,
                l.Pulsera.Alumno == null ? null
                    : l.Pulsera.Alumno.Nombres + " " + l.Pulsera.Alumno.ApellidoPaterno,
                l.Rssi, "dBm", l.Puesta, l.Sos, l.BateriaBaja,
                DateTime.SpecifyKind(l.Timestamp, DateTimeKind.Utc)))  // marcar como UTC para que el JSON lleve "Z"
            .ToListAsync();

        return Results.Ok(lecturas);
    }

    private static string? NombreCompleto(Alumno? a) =>
        a is null ? null : $"{a.Nombres} {a.ApellidoPaterno}";
}
