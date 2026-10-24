using Microsoft.EntityFrameworkCore;
using SafeBand.api.Data;
using SafeBand.api.Models;

namespace SafeBand.api.Servicios;

/// <summary>
/// Reglas de las alertas. Se llama con cada lectura que llega de un nodo.
/// SOS: la pulsera lo anuncia 2 min (para que algún nodo lo oiga); la alerta se abre con el primer aviso
/// y queda activa hasta que se desactiva desde la pulsera (bit "SOS cancelado") o desde la página.
/// </summary>
public static class Alertas
{
    /// <summary>
    /// Aplica las reglas a una lectura ya guardada (sin SaveChanges: lo hace quien llama).
    /// Devuelve la alerta nueva si se abrió una (para avisar a los tutores), o null.
    /// </summary>
    public static async Task<Alerta?> ProcesarLectura(AppDbContext db, int alumnoId, int zonaId, bool sos, bool sosCancelado, DateTime ahora)
    {
        var abierta = await db.Alertas.FirstOrDefaultAsync(a => a.AlumnoId == alumnoId && a.Tipo == TipoAlerta.Sos && a.Fin == null);

        // El niño la apagó desde la pulsera (volvió a mantener presionado el botón)
        if (sosCancelado)
        {
            if (abierta is not null)
            {
                abierta.Fin = ahora;
                abierta.DesactivadaDesde = OrigenDesactivacion.Pulsera;
            }
            return null;
        }

        if (!sos || abierta is not null) return null;

        // Si la escuela lo desactivó desde la página, la pulsera no se entera y sigue anunciando el MISMO SOS
        // hasta 2 min. Solo se abre otro cuando la pulsera ya dejó de mandarlo (hubo una lectura sin SOS después).
        var ultimoFin = await db.Alertas
            .Where(a => a.AlumnoId == alumnoId && a.Tipo == TipoAlerta.Sos && a.Fin != null)
            .MaxAsync(a => a.Fin);
        if (ultimoFin is not null
            && !await db.LecturasBle.AnyAsync(l => l.AlumnoId == alumnoId && l.Timestamp > ultimoFin && !l.Sos))
            return null;

        var nueva = new Alerta { Tipo = TipoAlerta.Sos, AlumnoId = alumnoId, ZonaId = zonaId, Inicio = ahora };
        db.Alertas.Add(nueva);
        return nueva;
    }

    /// <summary>Alumnos que tienen un SOS activo ahora.</summary>
    public static async Task<HashSet<int>> ConSosActivo(AppDbContext db) =>
        (await db.Alertas.AsNoTracking()
            .Where(a => a.Tipo == TipoAlerta.Sos && a.Fin == null)
            .Select(a => a.AlumnoId)
            .ToListAsync())
        .ToHashSet();
}
