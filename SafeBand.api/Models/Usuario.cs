using Microsoft.AspNetCore.Identity;

namespace SafeBand.api.Models;

/// <summary>
/// Cuenta para iniciar sesión en la app. Hereda de IdentityUser, que ya trae
/// Email, UserName, PasswordHash (contraseña cifrada), bloqueo por intentos, etc.
/// Aquí solo se agrega lo propio de SafeBand.
/// </summary>
public class Usuario : IdentityUser
{
    public string NombreMostrar { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; }

    /// <summary>
    /// Si la cuenta es de un padre/tutor, a qué registro de Tutores corresponde.
    /// Por aquí se sabe qué hijos puede ver (Tutor → TutoresAlumnos).
    /// </summary>
    public int? TutorId { get; set; }
    public Tutor? Tutor { get; set; }

    /// <summary>
    /// Cuándo aceptó el aviso de privacidad (LFPDPPP). Se pide al registrarse.
    /// Queda como evidencia del consentimiento del padre para tratar los datos de su hijo.
    /// </summary>
    public DateTime? AvisoPrivacidadAceptado { get; set; }

    /// <summary>Chat de Telegram donde recibe los avisos (null = no lo ha vinculado).</summary>
    public long? TelegramChatId { get; set; }

    /// <summary>Código del enlace para vincular Telegram (vence en 15 min; se borra al usarlo).</summary>
    public string? TelegramCodigo { get; set; }
    public DateTime? TelegramCodigoExpira { get; set; }
}
