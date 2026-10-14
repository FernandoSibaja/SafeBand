using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Configuración física de la escuela: zonas (salón, patio...) y los nodos ESP32 de cada una.
/// Por ahora sin inicio de sesión; se protegerá en el paso de usuarios y roles.
/// </summary>
public static class ZonasNodosEndpoints
{
    public static void MapZonasNodos(this WebApplication app)
    {
        var zonas = app.MapGroup("/api/zonas").WithTags("Zonas")
            .RequireAuthorization(Politicas.SoloAdministrador);
        zonas.MapGet("/", ListarZonas).WithSummary("Lista de zonas (?incluirInactivas=true)");
        zonas.MapGet("/{id:int}", ObtenerZona);
        zonas.MapPost("/", CrearZona).WithSummary("Da de alta una zona");
        zonas.MapPut("/{id:int}", EditarZona).WithSummary("Edita nombre, tipo o rectángulo en el plano de una zona");
        zonas.MapDelete("/{id:int}", DarDeBajaZona).WithSummary("Da de baja una zona (solo si ya no tiene nodos activos)");
        zonas.MapPost("/{id:int}/reactivar", ReactivarZona).WithSummary("Vuelve a activar una zona dada de baja");

        // El plano lo ve cualquier usuario con sesión (los padres lo usan en "Mis hijos").
        // Solo trae nombres y rectángulos de las zonas, nada de alumnos.
        app.MapGet("/api/plano", ObtenerPlano).WithTags("Zonas").RequireAuthorization()
            .WithSummary("Plano de la escuela: zonas activas con su rectángulo en metros");

        var nodos = app.MapGroup("/api/nodos").WithTags("Nodos")
            .RequireAuthorization(Politicas.SoloAdministrador);
        nodos.MapGet("/", ListarNodos).WithSummary("Lista de nodos con su zona y último contacto (?incluirInactivos=true)");
        nodos.MapGet("/{id:int}", ObtenerNodo);
        nodos.MapPost("/", CrearNodo).WithSummary("Da de alta un nodo (el código debe coincidir con el del firmware)");
        nodos.MapPut("/{id:int}", EditarNodo).WithSummary("Edita un nodo (cambiar de zona, umbral, descripción...)");
        nodos.MapDelete("/{id:int}", DarDeBajaNodo).WithSummary("Da de baja un nodo: la API rechaza sus lecturas");
        nodos.MapPost("/{id:int}/reactivar", ReactivarNodo).WithSummary("Vuelve a activar un nodo (su zona debe estar activa)");
    }

    // =================== Zonas ===================

    private static async Task<IResult> ListarZonas(AppDbContext db, bool? incluirInactivas)
    {
        var q = db.Zonas.AsNoTracking();
        if (incluirInactivas != true) q = q.Where(z => z.Activa);
        return Results.Ok(await ProyectarZonas(q.OrderBy(z => z.Nombre)).ToListAsync());
    }

    private static async Task<IResult> ObtenerZona(int id, AppDbContext db)
    {
        var zona = await ProyectarZonas(db.Zonas.AsNoTracking().Where(z => z.Id == id)).FirstOrDefaultAsync();
        return zona is null ? Results.NotFound(new { error = $"No existe la zona {id}." }) : Results.Ok(zona);
    }

    private static async Task<IResult> CrearZona(GuardarZonaRequest req, AppDbContext db)
    {
        var nombre = req.Nombre!.Trim();
        if (await db.Zonas.AnyAsync(z => z.Nombre == nombre))
            return Results.Conflict(new { error = $"Ya existe una zona llamada '{nombre}'." });

        var zona = new Zona { Nombre = nombre, Tipo = req.Tipo!.Value };
        AsignarPlano(zona, req.Plano);
        db.Zonas.Add(zona);
        await db.SaveChangesAsync();

        var respuesta = await ProyectarZonas(db.Zonas.Where(z => z.Id == zona.Id)).FirstAsync();
        return Results.Created($"/api/zonas/{zona.Id}", respuesta);
    }

    private static async Task<IResult> EditarZona(int id, GuardarZonaRequest req, AppDbContext db)
    {
        var zona = await db.Zonas.FindAsync(id);
        if (zona is null) return Results.NotFound(new { error = $"No existe la zona {id}." });

        var nombre = req.Nombre!.Trim();
        if (await db.Zonas.AnyAsync(z => z.Nombre == nombre && z.Id != id))
            return Results.Conflict(new { error = $"Ya existe otra zona llamada '{nombre}'." });

        zona.Nombre = nombre;
        zona.Tipo = req.Tipo!.Value;
        AsignarPlano(zona, req.Plano);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DarDeBajaZona(int id, AppDbContext db)
    {
        var zona = await db.Zonas.FindAsync(id);
        if (zona is null) return Results.NotFound(new { error = $"No existe la zona {id}." });

        // Una zona con nodos funcionando no se puede dar de baja: sus lecturas quedarían "huérfanas"
        if (await db.Nodos.AnyAsync(n => n.ZonaId == id && n.Activo))
            return Results.Conflict(new { error = $"La zona '{zona.Nombre}' tiene nodos activos. Muévelos a otra zona o dalos de baja primero." });

        zona.Activa = false;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ReactivarZona(int id, AppDbContext db)
    {
        var zona = await db.Zonas.FindAsync(id);
        if (zona is null) return Results.NotFound(new { error = $"No existe la zona {id}." });

        zona.Activa = true;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static IQueryable<ZonaResponse> ProyectarZonas(IQueryable<Zona> q) =>
        q.Select(z => new ZonaResponse(z.Id, z.Nombre, z.Tipo, z.Activa, z.Nodos.Count(n => n.Activo),
            z.PlanoX == null ? null : new RectanguloPlano(z.PlanoX, z.PlanoY, z.PlanoAncho, z.PlanoAlto)));

    // null = quitar la zona del plano
    private static void AsignarPlano(Zona zona, RectanguloPlano? plano)
    {
        zona.PlanoX = plano?.X;
        zona.PlanoY = plano?.Y;
        zona.PlanoAncho = plano?.Ancho;
        zona.PlanoAlto = plano?.Alto;
    }

    // GET /api/plano
    private static async Task<IResult> ObtenerPlano(AppDbContext db)
    {
        var zonas = await db.Zonas.AsNoTracking()
            .Where(z => z.Activa && z.PlanoX != null)
            .OrderBy(z => z.Nombre)
            .Select(z => new ZonaEnPlano(z.Id, z.Nombre, z.Tipo,
                z.PlanoX!.Value, z.PlanoY!.Value, z.PlanoAncho!.Value, z.PlanoAlto!.Value))
            .ToListAsync();

        // El plano mide lo que ocupe la zona más lejana (0 x 0 si todavía no hay zonas dibujadas)
        var ancho = zonas.Count == 0 ? 0 : zonas.Max(z => z.X + z.Ancho);
        var alto = zonas.Count == 0 ? 0 : zonas.Max(z => z.Y + z.Alto);
        return Results.Ok(new PlanoResponse(ancho, alto, zonas));
    }

    // =================== Nodos ===================

    private static async Task<IResult> ListarNodos(AppDbContext db, bool? incluirInactivos)
    {
        var q = db.Nodos.AsNoTracking();
        if (incluirInactivos != true) q = q.Where(n => n.Activo);
        return Results.Ok(await ProyectarNodos(q.OrderBy(n => n.Codigo)).ToListAsync());
    }

    private static async Task<IResult> ObtenerNodo(int id, AppDbContext db)
    {
        var nodo = await ProyectarNodos(db.Nodos.AsNoTracking().Where(n => n.Id == id)).FirstOrDefaultAsync();
        return nodo is null ? Results.NotFound(new { error = $"No existe el nodo {id}." }) : Results.Ok(nodo);
    }

    private static async Task<IResult> CrearNodo(GuardarNodoRequest req, AppDbContext db)
    {
        var codigo = req.Codigo!.Trim().ToLowerInvariant();
        if (await db.Nodos.AnyAsync(n => n.Codigo == codigo))
            return Results.Conflict(new { error = $"Ya existe un nodo con el código '{codigo}'." });

        var errorZona = await ValidarZona(db, req.ZonaId!.Value);
        if (errorZona is not null) return Results.BadRequest(new { error = errorZona });

        var nodo = new Nodo { Codigo = codigo };
        AplicarNodo(nodo, req);
        db.Nodos.Add(nodo);
        await db.SaveChangesAsync();

        var respuesta = await ProyectarNodos(db.Nodos.Where(n => n.Id == nodo.Id)).FirstAsync();
        return Results.Created($"/api/nodos/{nodo.Id}", respuesta);
    }

    private static async Task<IResult> EditarNodo(int id, GuardarNodoRequest req, AppDbContext db)
    {
        var nodo = await db.Nodos.FindAsync(id);
        if (nodo is null) return Results.NotFound(new { error = $"No existe el nodo {id}." });

        var codigo = req.Codigo!.Trim().ToLowerInvariant();
        if (await db.Nodos.AnyAsync(n => n.Codigo == codigo && n.Id != id))
            return Results.Conflict(new { error = $"Ya existe otro nodo con el código '{codigo}'." });

        var errorZona = await ValidarZona(db, req.ZonaId!.Value);
        if (errorZona is not null) return Results.BadRequest(new { error = errorZona });

        nodo.Codigo = codigo;
        AplicarNodo(nodo, req);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DarDeBajaNodo(int id, AppDbContext db)
    {
        var nodo = await db.Nodos.FindAsync(id);
        if (nodo is null) return Results.NotFound(new { error = $"No existe el nodo {id}." });

        nodo.Activo = false;   // desde ahora, POST /api/lecturas de este nodo responde 400
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ReactivarNodo(int id, AppDbContext db)
    {
        var nodo = await db.Nodos.Include(n => n.Zona).FirstOrDefaultAsync(n => n.Id == id);
        if (nodo is null) return Results.NotFound(new { error = $"No existe el nodo {id}." });
        if (!nodo.Zona.Activa)
            return Results.BadRequest(new { error = $"La zona '{nodo.Zona.Nombre}' está dada de baja. Reactívala o mueve el nodo a otra zona primero." });

        nodo.Activo = true;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<string?> ValidarZona(AppDbContext db, int zonaId)
    {
        var zona = await db.Zonas.FindAsync(zonaId);
        if (zona is null) return $"La zona {zonaId} no existe.";
        if (!zona.Activa) return $"La zona '{zona.Nombre}' está dada de baja.";
        return null;
    }

    private static void AplicarNodo(Nodo nodo, GuardarNodoRequest req)
    {
        nodo.ZonaId = req.ZonaId!.Value;
        nodo.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        nodo.UmbralRssi = req.UmbralRssi;
        nodo.EsPortatil = req.EsPortatil;
    }

    private static IQueryable<NodoResponse> ProyectarNodos(IQueryable<Nodo> q) =>
        q.Select(n => new NodoResponse(
            n.Id, n.Codigo, n.Descripcion, n.ZonaId, n.Zona.Nombre, n.UmbralRssi, n.EsPortatil, n.Activo,
            n.UltimoContacto == null ? null : DateTime.SpecifyKind(n.UltimoContacto.Value, DateTimeKind.Utc)));
}
