using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Cuentas del personal de la escuela (solo Administrador): maestros y administradores.
/// La cuenta se crea sin contraseña; la persona la activa con un código de invitación
/// (el mismo mecanismo que los padres, en "Crear mi cuenta"). Un código nuevo también sirve
/// para que alguien del personal que olvidó su contraseña ponga una nueva.
/// </summary>
public static class PersonalEndpoints
{
    private static readonly string[] RolesPersonal = [Roles.Administrador, Roles.Maestro];

    public static void MapPersonal(this WebApplication app)
    {
        var personal = app.MapGroup("/api/personal").WithTags("Personal")
            .RequireAuthorization(Politicas.SoloAdministrador);

        personal.MapGet("/", Listar).WithSummary("Maestros y administradores (?incluirInactivos=true)");
        personal.MapPost("/", Crear).WithSummary("Da de alta una cuenta del personal (sin contraseña; luego se invita)");
        personal.MapPut("/{id}", Editar).WithSummary("Edita nombre, correo o rol");
        personal.MapDelete("/{id}", DarDeBaja).WithSummary("Da de baja la cuenta: ya no puede iniciar sesión");
        personal.MapPost("/{id}/reactivar", Reactivar).WithSummary("Vuelve a activar una cuenta dada de baja");
        personal.MapPost("/{id}/invitacion", GenerarInvitacion)
            .WithSummary("Código de un solo uso (7 días) para activar la cuenta o poner una contraseña nueva");
    }

    // GET /api/personal
    private static async Task<IResult> Listar(ClaimsPrincipal yo, AppDbContext db, UserManager<Usuario> usuarios, bool? incluirInactivos)
    {
        var miId = usuarios.GetUserId(yo);
        var ahora = DateTime.UtcNow;
        var lista = new List<PersonalResponse>();
        foreach (var rol in RolesPersonal)
        {
            foreach (var u in await usuarios.GetUsersInRoleAsync(rol))
            {
                if (incluirInactivos != true && !u.Activo) continue;
                var grupos = await db.Grupos.Where(g => g.MaestroId == u.Id && g.Activo).OrderBy(g => g.Nombre).Select(g => g.Nombre).ToListAsync();
                var estado = u.PasswordHash is not null ? EstadosCuenta.CuentaActiva
                    : await db.Invitaciones.AnyAsync(i => i.UsuarioId == u.Id && i.UsadaEn == null && i.Expira > ahora) ? EstadosCuenta.InvitacionPendiente
                    : EstadosCuenta.SinInvitar;
                lista.Add(new PersonalResponse(u.Id, u.NombreMostrar, u.Email!, rol, u.Activo, estado, grupos, u.Id == miId));
            }
        }
        return Results.Ok(lista.OrderBy(p => p.Nombre).ToList());
    }

    // POST /api/personal
    private static async Task<IResult> Crear(GuardarPersonalRequest req, UserManager<Usuario> usuarios)
    {
        var email = req.Email!.Trim().ToLowerInvariant();
        if (await usuarios.FindByEmailAsync(email) is not null)
            return Results.Conflict(new { error = $"Ya existe una cuenta con el correo {email}." });

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,   // lo registra la escuela
            NombreMostrar = req.Nombre!.Trim(),
            FechaAlta = DateTime.UtcNow,
        };
        var creado = await usuarios.CreateAsync(usuario);   // sin contraseña: la pondrá con su invitación
        if (!creado.Succeeded)
            return Results.BadRequest(new { error = string.Join(" ", creado.Errors.Select(e => e.Description)) });
        var rol = await usuarios.AddToRoleAsync(usuario, req.Rol!);
        if (!rol.Succeeded)
        {
            await usuarios.DeleteAsync(usuario);
            return Results.Problem("No se pudo crear la cuenta. Intenta de nuevo.");
        }
        return Results.Created($"/api/personal/{usuario.Id}",
            new PersonalResponse(usuario.Id, usuario.NombreMostrar, email, req.Rol!, true, EstadosCuenta.SinInvitar, [], false));
    }

    // PUT /api/personal/{id}
    private static async Task<IResult> Editar(string id, GuardarPersonalRequest req, ClaimsPrincipal yo, AppDbContext db, UserManager<Usuario> usuarios)
    {
        var (usuario, rolActual, error) = await Buscar(id, usuarios);
        if (error is not null) return error;

        var email = req.Email!.Trim().ToLowerInvariant();
        var otro = await usuarios.FindByEmailAsync(email);
        if (otro is not null && otro.Id != id)
            return Results.Conflict(new { error = $"Ya existe otra cuenta con el correo {email}." });

        if (rolActual != req.Rol)
        {
            // Nadie se quita a sí mismo el rol de Administrador (se quedaría sin acceso a esta pantalla)
            if (usuarios.GetUserId(yo) == id)
                return Results.BadRequest(new { error = "No puedes cambiar tu propio rol. Pídeselo a otro administrador." });
            if (rolActual == Roles.Maestro && await db.Grupos.AnyAsync(g => g.MaestroId == id && g.Activo))
                return Results.Conflict(new { error = "Es maestro titular de algún grupo. Asigna otro maestro a ese grupo antes de cambiarle el rol." });
            await usuarios.RemoveFromRoleAsync(usuario!, rolActual!);
            await usuarios.AddToRoleAsync(usuario!, req.Rol!);
            await usuarios.UpdateSecurityStampAsync(usuario!);   // sus permisos cambian: que vuelva a iniciar sesión
        }

        usuario!.NombreMostrar = req.Nombre!.Trim();
        usuario.Email = email;
        usuario.UserName = email;
        var r = await usuarios.UpdateAsync(usuario);
        return r.Succeeded ? Results.NoContent()
            : Results.BadRequest(new { error = string.Join(" ", r.Errors.Select(e => e.Description)) });
    }

    // DELETE /api/personal/{id} → baja lógica: Activo = false (el login ya rechaza cuentas inactivas)
    private static async Task<IResult> DarDeBaja(string id, ClaimsPrincipal yo, AppDbContext db, UserManager<Usuario> usuarios)
    {
        var (usuario, _, error) = await Buscar(id, usuarios);
        if (error is not null) return error;
        if (usuarios.GetUserId(yo) == id)
            return Results.BadRequest(new { error = "No puedes dar de baja tu propia cuenta." });

        var grupos = await db.Grupos.Where(g => g.MaestroId == id && g.Activo).Select(g => g.Nombre).ToListAsync();
        if (grupos.Count > 0)
            return Results.Conflict(new { error = $"Es maestro titular de {string.Join(", ", grupos)}. Asigna otro maestro a ese grupo primero." });

        usuario!.Activo = false;
        await usuarios.UpdateAsync(usuario);
        await usuarios.UpdateSecurityStampAsync(usuario);   // cierra sus sesiones abiertas
        // Un código pendiente ya no debe servir
        db.Invitaciones.RemoveRange(db.Invitaciones.Where(i => i.UsuarioId == id && i.UsadaEn == null));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // POST /api/personal/{id}/reactivar
    private static async Task<IResult> Reactivar(string id, UserManager<Usuario> usuarios)
    {
        var (usuario, _, error) = await Buscar(id, usuarios);
        if (error is not null) return error;
        usuario!.Activo = true;
        await usuarios.UpdateAsync(usuario);
        return Results.NoContent();
    }

    // POST /api/personal/{id}/invitacion → código nuevo (anula los anteriores sin usar)
    private static async Task<IResult> GenerarInvitacion(string id, AppDbContext db, UserManager<Usuario> usuarios)
    {
        var (usuario, _, error) = await Buscar(id, usuarios);
        if (error is not null) return error;
        if (!usuario!.Activo)
            return Results.BadRequest(new { error = "La cuenta está dada de baja; reactívala para invitarla." });

        db.Invitaciones.RemoveRange(await db.Invitaciones.Where(i => i.UsuarioId == id && i.UsadaEn == null).ToListAsync());
        var codigo = CodigosInvitacion.Generar();
        var invitacion = new Invitacion
        {
            UsuarioId = id,
            Creada = DateTime.UtcNow,
            Expira = DateTime.UtcNow.Add(CodigosInvitacion.Vigencia),
        };
        invitacion.CodigoHash = CodigosInvitacion.Hashear(invitacion, codigo);
        db.Invitaciones.Add(invitacion);
        await db.SaveChangesAsync();

        // El código en claro solo se devuelve AHORA
        return Results.Ok(new InvitacionResponse(codigo, invitacion.Expira, usuario.Email!, usuario.NombreMostrar));
    }

    // ---------- Ayudantes ----------

    /// <summary>La cuenta, si existe y es del personal (no se administran cuentas de padres aquí).</summary>
    private static async Task<(Usuario?, string?, IResult?)> Buscar(string id, UserManager<Usuario> usuarios)
    {
        var usuario = await usuarios.FindByIdAsync(id);
        if (usuario is null) return (null, null, Results.NotFound(new { error = "No existe esa cuenta." }));
        var rol = (await usuarios.GetRolesAsync(usuario)).FirstOrDefault(r => RolesPersonal.Contains(r));
        return rol is null
            ? (null, null, Results.NotFound(new { error = "Esa cuenta no es del personal de la escuela." }))
            : (usuario, rol, null);
    }
}
