namespace SafeBand.api.Models;

/// <summary>
/// Código de un solo uso que la escuela entrega para crear una cuenta en la app:
/// a un tutor (se crea su cuenta de Padre) o a alguien del personal (activa su cuenta ya creada
/// o pone una contraseña nueva). Lleva TutorId o UsuarioId, nunca los dos.
/// El código NO se guarda tal cual: solo su hash (como las contraseñas).
/// </summary>
public class Invitacion
{
    public int Id { get; set; }

    public int? TutorId { get; set; }
    public Tutor? Tutor { get; set; }

    /// <summary>Cuenta del personal (maestro o administrador) que se activa con este código.</summary>
    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Hash del código (PBKDF2 con sal). Ni viendo la base de datos se puede saber el código.</summary>
    public string CodigoHash { get; set; } = string.Empty;

    public DateTime Creada { get; set; }

    /// <summary>Después de esta fecha el código ya no sirve.</summary>
    public DateTime Expira { get; set; }

    /// <summary>Cuándo se usó para crear la cuenta (null = todavía no se usa).</summary>
    public DateTime? UsadaEn { get; set; }
}
