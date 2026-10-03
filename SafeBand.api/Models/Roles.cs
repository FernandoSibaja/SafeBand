namespace SafeBand.api.Models;

/// <summary>Nombres de los roles. Se usan como constantes para no escribirlos a mano (y equivocarse).</summary>
public static class Roles
{
    public const string Administrador = "Administrador";   // ve y administra todo
    public const string Maestro = "Maestro";               // ve a su grupo, pase de lista
    public const string Padre = "Padre";                   // ve solo a sus hijos

    public static readonly string[] Todos = [Administrador, Maestro, Padre];
}

/// <summary>
/// Políticas de autorización: reglas con nombre que se ponen a los endpoints
/// (ej. .RequireAuthorization(Politicas.SoloAdministrador)). Se definen en Program.cs.
/// </summary>
public static class Politicas
{
    public const string SoloAdministrador = "SoloAdministrador";
}
