using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Contracts;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Inicio y cierre de sesión. Al iniciar sesión, la API deja una cookie cifrada en el navegador
/// ("SafeBand.Sesion") que se manda sola en cada petición; así la API sabe quién eres.
/// </summary>
public static class AuthEndpoints
{
    // Mismo mensaje para "correo no existe" y "contraseña incorrecta": no revela qué correos están registrados
    private const string CredencialesInvalidas = "Correo o contraseña incorrectos.";

    public static void MapAuth(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/auth").WithTags("Sesión");

        // "auth": máximo 10 intentos por minuto por dirección IP (frena a quien intente adivinar contraseñas o códigos)
        grupo.MapPost("/login", Login).RequireRateLimiting("auth")
            .WithSummary("Inicia sesión con correo y contraseña (deja la cookie de sesión)");
        grupo.MapPost("/registro", Registro).RequireRateLimiting("auth")
            .WithSummary("Un padre crea su cuenta con el código de invitación que le dio la escuela");
        grupo.MapPost("/logout", Logout).WithSummary("Cierra la sesión (borra la cookie)");
        grupo.MapGet("/yo", QuienSoy).RequireAuthorization().WithSummary("Datos y roles del usuario con sesión iniciada");
    }

    // Mismo mensaje si el correo no es de ningún tutor o si el código no sirve: no revela qué correos existen
    private const string InvitacionInvalida =
        "El correo o el código de invitación no son válidos, o el código ya venció. Pide uno nuevo a la escuela.";

    // POST /api/auth/registro
    private static async Task<IResult> Registro(
        RegistroRequest req, AppDbContext db, UserManager<Usuario> usuarios, SignInManager<Usuario> sesion)
    {
        if (!req.AceptaAvisoPrivacidad)
            return Results.BadRequest(new { error = "Para crear tu cuenta debes aceptar el aviso de privacidad." });

        var email = req.Email!.Trim().ToLowerInvariant();

        // 1. El correo debe ser el de un tutor activo que la escuela registró
        var tutor = await db.Tutores.FirstOrDefaultAsync(t => t.Email == email && t.Activo);
        if (tutor is null) return Results.BadRequest(new { error = InvitacionInvalida });

        // 2. Y el código debe coincidir con una invitación vigente de ESE tutor
        var ahora = DateTime.UtcNow;
        var pendientes = await db.Invitaciones
            .Where(i => i.TutorId == tutor.Id && i.UsadaEn == null && i.Expira > ahora)
            .ToListAsync();
        var invitacion = pendientes.FirstOrDefault(i => CodigosInvitacion.Verificar(i, req.Codigo!));
        if (invitacion is null) return Results.BadRequest(new { error = InvitacionInvalida });

        if (await db.Users.AnyAsync(u => u.TutorId == tutor.Id))
            return Results.Conflict(new { error = "Ya existe una cuenta para este correo. Inicia sesión." });

        // 3. Crear la cuenta ligada al tutor (por ahí sabrá qué hijos puede ver)
        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,   // la escuela ya verificó el correo al registrar al tutor
            NombreMostrar = $"{tutor.Nombres} {tutor.ApellidoPaterno}",
            TutorId = tutor.Id,
            FechaAlta = ahora,
            AvisoPrivacidadAceptado = ahora,
        };
        var creado = await usuarios.CreateAsync(usuario, req.Password!);
        if (!creado.Succeeded)
            return Results.BadRequest(new { error = string.Join(" ", creado.Errors.Select(e => e.Description)) });

        var rol = await usuarios.AddToRoleAsync(usuario, Roles.Padre);
        if (!rol.Succeeded)
        {
            await usuarios.DeleteAsync(usuario);   // no dejar una cuenta a medias
            return Results.Problem("No se pudo completar el registro. Intenta de nuevo.");
        }

        // 4. El código ya no se puede volver a usar
        invitacion.UsadaEn = ahora;
        await db.SaveChangesAsync();

        // 5. Entra directo a la app
        await sesion.SignInAsync(usuario, isPersistent: true);
        return Results.Created("/api/auth/yo", await Describir(usuario, usuarios));
    }

    // POST /api/auth/login
    private static async Task<IResult> Login(
        LoginRequest req, UserManager<Usuario> usuarios, SignInManager<Usuario> sesion)
    {
        var usuario = await usuarios.FindByEmailAsync(req.Email!.Trim());
        if (usuario is null || !usuario.Activo)
            return Results.Json(new { error = CredencialesInvalidas }, statusCode: StatusCodes.Status401Unauthorized);

        // lockoutOnFailure: true → cada intento fallido cuenta para el bloqueo (5 intentos → 15 min)
        var resultado = await sesion.PasswordSignInAsync(usuario, req.Password!, req.Recordarme, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
            return Results.Json(
                new { error = "Cuenta bloqueada temporalmente por demasiados intentos fallidos. Intenta de nuevo en 15 minutos." },
                statusCode: StatusCodes.Status401Unauthorized);

        if (!resultado.Succeeded)
            return Results.Json(new { error = CredencialesInvalidas }, statusCode: StatusCodes.Status401Unauthorized);

        return Results.Ok(await Describir(usuario, usuarios));
    }

    // POST /api/auth/logout
    private static async Task<IResult> Logout(SignInManager<Usuario> sesion)
    {
        await sesion.SignOutAsync();
        return Results.NoContent();
    }

    // GET /api/auth/yo   (requiere sesión: sin cookie responde 401)
    private static async Task<IResult> QuienSoy(ClaimsPrincipal yo, UserManager<Usuario> usuarios)
    {
        var usuario = await usuarios.GetUserAsync(yo);
        if (usuario is null || !usuario.Activo)
            return Results.Json(new { error = "La sesión ya no es válida." }, statusCode: StatusCodes.Status401Unauthorized);

        return Results.Ok(await Describir(usuario, usuarios));
    }

    private static async Task<UsuarioActualResponse> Describir(Usuario u, UserManager<Usuario> usuarios) =>
        new(u.Id, u.Email!, u.NombreMostrar, (await usuarios.GetRolesAsync(u)).ToList(), u.TutorId);
}
