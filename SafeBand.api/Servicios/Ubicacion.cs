namespace SafeBand.api.Servicios;

/// <summary>
/// Reglas para decidir DÓNDE está una pulsera cuando varios nodos la oyen a la vez.
/// Cada nodo manda su propia lectura (una por ventana de 10 s), así que una misma pulsera puede
/// llegar desde 2 o 3 nodos casi al mismo tiempo. Se elige la del nodo con la señal MÁS FUERTE:
/// el nodo del cuarto donde está el niño casi siempre la oye mejor que uno detrás de un muro o una losa.
/// Lo usan "Mis hijos" (zona actual y recorrido) y lo usarán la triangulación y el modo búsqueda.
/// </summary>
public static class Ubicacion
{
    /// <summary>
    /// Lecturas que se consideran "del mismo momento": una ventana del nodo (10 s) más el retraso de red.
    /// </summary>
    public static readonly TimeSpan Simultaneas = TimeSpan.FromSeconds(15);

    /// <summary>Lo mínimo de una lectura que hace falta para decidir la zona.</summary>
    public record Lectura(DateTime Hora, int ZonaId, string Zona, int Rssi, int UmbralRssi, bool Puesta, bool Sos, bool BateriaBaja);

    /// <summary>
    /// De un grupo de lecturas simultáneas, la del nodo con la señal más fuerte.
    /// Si cualquiera trae SOS o batería baja, se conserva (no se pierde un SOS por elegir otro nodo).
    /// "Puesta" se toma de la lectura más reciente.
    /// </summary>
    public static Lectura MasFuerte(IReadOnlyCollection<Lectura> grupo)
    {
        var fuerte = grupo.OrderByDescending(l => l.Rssi).ThenByDescending(l => l.Hora).First();
        var reciente = grupo.MaxBy(l => l.Hora)!;
        return fuerte with
        {
            Hora = reciente.Hora,
            Puesta = reciente.Puesta,
            Sos = grupo.Any(l => l.Sos),
            BateriaBaja = grupo.Any(l => l.BateriaBaja),
        };
    }

    /// <summary>¿La señal está por debajo del umbral de su nodo? (cerca de la zona, pero no claramente dentro)</summary>
    public static bool EsDebil(Lectura l) => l.Rssi < l.UmbralRssi;

    /// <summary>
    /// Agrupa lecturas ordenadas por hora en bloques de lecturas simultáneas y devuelve la más fuerte de cada uno.
    /// Un bloque empieza con una lectura y abarca las que llegan dentro de <see cref="Simultaneas"/>.
    /// </summary>
    public static List<Lectura> PorMomento(IEnumerable<Lectura> lecturasOrdenadas)
    {
        var resultado = new List<Lectura>();
        var bloque = new List<Lectura>();
        foreach (var l in lecturasOrdenadas)
        {
            if (bloque.Count > 0 && l.Hora - bloque[0].Hora > Simultaneas)
            {
                resultado.Add(MasFuerte(bloque));
                bloque.Clear();
            }
            bloque.Add(l);
        }
        if (bloque.Count > 0) resultado.Add(MasFuerte(bloque));
        return resultado;
    }
}
