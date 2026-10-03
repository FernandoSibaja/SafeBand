using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Models;

namespace SafeBand.api.Data;

/// <summary>
/// Datos iniciales. Corre al arrancar la API (en tu PC y en Azure), solo si la tabla Zonas está vacía.
/// Así el ESP32 puede mandar lecturas de "nodo-1" / "SB-0001" desde el primer momento.
/// </summary>
public static class DbSeeder
{
    public static async Task SembrarAsync(AppDbContext db)
    {
        // Si ya hay datos, no hace nada (así no se duplican cada vez que arrancas la API)
        if (await db.Zonas.AnyAsync()) return;

        var salon = new Zona { Nombre = "Salón 3°A", Tipo = TipoZona.Salon };

        var nodo = new Nodo
        {
            Codigo = "nodo-1",
            Descripcion = "ESP32 propio (prueba de concepto)",
            Zona = salon            // EF pone el ZonaId automáticamente al guardar
        };

        var pulsera = new Pulsera
        {
            IdentificadorBle = "SB-0001",   // el celular con nRF Connect anunciará este nombre
            FechaAlta = DateTime.UtcNow
        };

        db.Zonas.Add(salon);
        db.Nodos.Add(nodo);
        db.Pulseras.Add(pulsera);
        await db.SaveChangesAsync();        // aquí se ejecutan los INSERT en SQL Server
    }

    /// <summary>
    /// Crea los 3 roles si no existen y, si todavía no hay ningún administrador, crea uno
    /// con el correo y contraseña de la configuración "AdminInicial" (NUNCA escritos en el código:
    /// en tu PC vienen de "user secrets"; en Azure, de la configuración del App Service).
    /// </summary>
    public static async Task SembrarUsuariosAsync(IServiceProvider servicios, IConfiguration config, ILogger logger)
    {
        var roles = servicios.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in Roles.Todos)
        {
            if (!await roles.RoleExistsAsync(rol))
                await roles.CreateAsync(new IdentityRole(rol));
        }

        var usuarios = servicios.GetRequiredService<UserManager<Usuario>>();
        if ((await usuarios.GetUsersInRoleAsync(Roles.Administrador)).Count > 0) return;   // ya hay admin

        var email = config["AdminInicial:Email"];
        var password = config["AdminInicial:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No hay administrador y falta la configuración AdminInicial:Email / AdminInicial:Password. " +
                              "Configúrala con 'dotnet user-secrets' (ver MANUAL.md).");
            return;
        }

        var admin = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreMostrar = "Administrador",
            FechaAlta = DateTime.UtcNow
        };
        var resultado = await usuarios.CreateAsync(admin, password);
        if (!resultado.Succeeded)
        {
            logger.LogError("No se pudo crear el administrador inicial: {Errores}",
                string.Join("; ", resultado.Errors.Select(e => e.Description)));
            return;
        }
        await usuarios.AddToRoleAsync(admin, Roles.Administrador);
        logger.LogInformation("Administrador inicial creado: {Email}", email);
    }
}
