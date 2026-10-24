using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Nombre y teléfono de la escuela. Cualquiera con sesión los lee (el padre necesita el teléfono);
/// solo el Administrador los cambia.
/// </summary>
public static class DatosEscuelaEndpoints
{
    public record DatosEscuelaDto(
        [property: Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
        [property: StringLength(150, ErrorMessage = "El campo 'nombre' admite máximo 150 caracteres.")]
        string? Nombre,

        [property: RegularExpression(@"^[0-9 +()\-]{7,20}$", ErrorMessage = "El teléfono solo admite números, espacios, +, ( ) y guiones (7 a 20 caracteres).")]
        string? Telefono = null);

    public static void MapDatosEscuela(this WebApplication app)
    {
        var datos = app.MapGroup("/api/datos-escuela").WithTags("Escuela");
        datos.MapGet("/", Obtener).RequireAuthorization().WithSummary("Nombre y teléfono de la escuela");
        datos.MapPut("/", Guardar).RequireAuthorization(Politicas.SoloAdministrador).WithSummary("Cambia el nombre y el teléfono de la escuela");
    }

    // GET /api/datos-escuela
    private static async Task<IResult> Obtener(AppDbContext db)
    {
        var d = await db.DatosEscuela.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
        return Results.Ok(new DatosEscuelaDto(d?.Nombre ?? "", d?.Telefono));
    }

    // PUT /api/datos-escuela
    private static async Task<IResult> Guardar(DatosEscuelaDto req, AppDbContext db)
    {
        var d = await db.DatosEscuela.FirstOrDefaultAsync(x => x.Id == 1);
        if (d is null) db.DatosEscuela.Add(d = new DatosEscuela { Id = 1 });
        d.Nombre = req.Nombre!.Trim();
        d.Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
