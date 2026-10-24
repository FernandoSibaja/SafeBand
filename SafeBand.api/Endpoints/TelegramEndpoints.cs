using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SafeBand.api.Data;
using SafeBand.api.Models;
using SafeBand.api.Servicios;

namespace SafeBand.api.Endpoints;

/// <summary>
/// Vincular la cuenta con Telegram para recibir avisos. Cada quien solo vincula SU cuenta.
/// </summary>
public static class TelegramEndpoints
{
    public record EstadoTelegram(bool Disponible, bool Vinculado, string? Bot);
    public record EnlaceTelegram(string Enlace, DateTime Expira);

    public static void MapTelegram(this WebApplication app)
    {
        var t = app.MapGroup("/api/telegram").WithTags("Avisos").RequireAuthorization();
        t.MapGet("/", Estado).WithSummary("¿Está vinculada mi cuenta con Telegram?");
        t.MapPost("/vincular", Vincular).WithSummary("Enlace t.me para vincular mi cuenta (vence en 15 min)");
        t.MapPost("/revisar", Revisar).WithSummary("Revisa si ya toqué «Iniciar» en Telegram y devuelve el estado");
        t.MapDelete("/", Desvincular).WithSummary("Deja de recibir avisos por Telegram");
    }

    // GET /api/telegram
    private static async Task<IResult> Estado(ClaimsPrincipal yo, UserManager<Usuario> usuarios, TelegramServicio telegram)
    {
        var u = await usuarios.GetUserAsync(yo);
        return Results.Ok(new EstadoTelegram(telegram.Disponible, u?.TelegramChatId is not null, telegram.Bot));
    }

    // POST /api/telegram/vincular
    private static async Task<IResult> Vincular(ClaimsPrincipal yo, UserManager<Usuario> usuarios, TelegramServicio telegram)
    {
        if (!telegram.Disponible)
            return Results.Problem("Los avisos por Telegram no están configurados en el servidor.", statusCode: StatusCodes.Status503ServiceUnavailable);
        var u = await usuarios.GetUserAsync(yo);
        if (u is null) return Results.Unauthorized();

        u.TelegramCodigo = TelegramServicio.NuevoCodigo();
        u.TelegramCodigoExpira = DateTime.UtcNow.Add(TelegramServicio.VigenciaCodigo);
        await usuarios.UpdateAsync(u);
        return Results.Ok(new EnlaceTelegram(telegram.Enlace(u.TelegramCodigo), u.TelegramCodigoExpira.Value));
    }

    // POST /api/telegram/revisar
    private static async Task<IResult> Revisar(ClaimsPrincipal yo, UserManager<Usuario> usuarios, AppDbContext db, TelegramServicio telegram)
    {
        await telegram.RevisarMensajes(db);
        var id = usuarios.GetUserId(yo);
        var u = await db.Users.FindAsync(id);
        if (u is not null) await db.Entry(u).ReloadAsync();   // lo pudo vincular la revisión de otra petición
        return Results.Ok(new EstadoTelegram(telegram.Disponible, u?.TelegramChatId is not null, telegram.Bot));
    }

    // DELETE /api/telegram
    private static async Task<IResult> Desvincular(ClaimsPrincipal yo, UserManager<Usuario> usuarios, TelegramServicio telegram)
    {
        var u = await usuarios.GetUserAsync(yo);
        if (u is null) return Results.Unauthorized();
        var chat = u.TelegramChatId;
        u.TelegramChatId = null;
        u.TelegramCodigo = null;
        u.TelegramCodigoExpira = null;
        await usuarios.UpdateAsync(u);
        if (chat is not null) await telegram.Enviar(chat.Value, "Ya no recibirás avisos de SafeBand por aquí. Puedes volver a activarlos desde la página.");
        return Results.NoContent();
    }
}
