using Microsoft.EntityFrameworkCore;
using SafeBand.api.Models;

namespace SafeBand.api.Data;

/// <summary>
/// Datos iniciales de prueba. Solo corre en desarrollo (tu PC) y solo si la tabla Zonas está vacía.
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
}
