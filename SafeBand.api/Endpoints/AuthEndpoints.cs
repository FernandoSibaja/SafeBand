using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SafeBand.api.Contracts;
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

        grupo.MapPost("/login", Login).WithSummary("Inicia sesión con correo y contraseña (deja la cookie de sesión)");
        grupo.MapPost("/logout", Logout).WithSummary("Cierra la sesión (borra la cookie)");
        grupo.MapGet("/yo", QuienSoy).RequireAuthorization().WithSummary("Datos y roles del usuario con sesión iniciada");
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
