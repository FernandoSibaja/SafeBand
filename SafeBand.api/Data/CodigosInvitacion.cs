using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using SafeBand.api.Models;

namespace SafeBand.api.Data;

/// <summary>
/// Genera y verifica los códigos de invitación (ej. "K7M4-9QPX").
/// </summary>
public static class CodigosInvitacion
{
    // Sin letras ni números que se confunden al leer un papel impreso (0/O, 1/I/L)
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static readonly TimeSpan Vigencia = TimeSpan.FromDays(7);

    // Mismo algoritmo que las contraseñas (PBKDF2 con sal aleatoria)
    private static readonly PasswordHasher<Invitacion> Hasher = new();

    /// <summary>8 caracteres aleatorios criptográficamente seguros, en formato XXXX-XXXX.</summary>
    public static string Generar()
    {
        var c = new char[8];
        for (var i = 0; i < c.Length; i++)
            c[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return $"{new string(c, 0, 4)}-{new string(c, 4, 4)}";
    }

    public static string Hashear(Invitacion invitacion, string codigo) =>
        Hasher.HashPassword(invitacion, Normalizar(codigo));

    public static bool Verificar(Invitacion invitacion, string codigo) =>
        Hasher.VerifyHashedPassword(invitacion, invitacion.CodigoHash, Normalizar(codigo)) != PasswordVerificationResult.Failed;

    // "k7m4 9qpx" o "K7M4-9QPX" → "K7M49QPX"
    private static string Normalizar(string codigo) =>
        new string(codigo.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
