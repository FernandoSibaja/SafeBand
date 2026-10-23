using SafeBand.api.Models;

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

    /// <summary>Lo mínimo de una lectura que hace falta para decidir la zona (y, si el nodo tiene posición, triangular).</summary>
    public record Lectura(DateTime Hora, int ZonaId, string Zona, int Rssi, int UmbralRssi, bool Puesta, bool Sos, bool BateriaBaja,
        int NodoId = 0, double? NodoX = null, double? NodoY = null);

    /// <summary>
    /// Modelo de propagación de la prueba de alcance: RSSI(d) = Rssi1m − 10·n·log10(d).
    /// Se configura en appsettings.json → "Ubicacion".
    /// </summary>
    public record Calibracion(double Rssi1m, double N);

    /// <summary>Posición estimada en el plano (metros), con su radio de incertidumbre y cuántos nodos se usaron.</summary>
    public record Posicion(double X, double Y, double Radio, int Nodos);

    private const double DistanciaMin = 0.3, DistanciaMax = 30, RadioMin = 1.5, RadioMax = 12;

    /// <summary>Lecturas más débiles que la más fuerte por más de esto (dB) no se usan: el ruido las vuelve metros de error.</summary>
    private const double MargenDb = 12;

    /// <summary>Holgura (m) que se permite al punto más allá de la distancia que indica el nodo más fuerte.</summary>
    private const double HolguraFuerte = 2;

    /// <summary>Distancia aproximada (m) que corresponde a un RSSI, según la calibración.</summary>
    public static double Distancia(double rssi, Calibracion c) =>
        Math.Clamp(Math.Pow(10, (c.Rssi1m - rssi) / (10 * c.N)), DistanciaMin, DistanciaMax);

    /// <summary>
    /// Estima la posición de la pulsera con las lecturas simultáneas de los nodos que tienen posición en el plano.
    /// 3+ nodos: mínimos cuadrados (Gauss-Newton) · 2 nodos: punto entre ambos · 1 nodo: el nodo, con la distancia de radio.
    /// Se ignoran los nodos que la oyen mucho peor que el más fuerte, y el punto nunca se aleja de él más de lo que indica su señal.
    /// Devuelve null si ningún nodo tiene posición. Si se pasan medidas del plano, el punto se mantiene dentro.
    /// </summary>
    public static Posicion? Estimar(IEnumerable<Lectura> grupo, Calibracion c, double? anchoPlano = null, double? altoPlano = null)
    {
        // Una medición por nodo: el promedio de sus lecturas en el grupo
        var medidas = grupo.Where(l => l.NodoX is not null && l.NodoY is not null)
            .GroupBy(l => l.NodoId)
            .Select(g => (X: g.First().NodoX!.Value, Y: g.First().NodoY!.Value, Rssi: g.Average(l => l.Rssi)))
            .OrderByDescending(m => m.Rssi)
            .ToList();
        if (medidas.Count == 0) return null;

        // Solo los nodos que la oyen casi tan bien como el más fuerte (el primero de la lista)
        var nodos = medidas.Where(m => m.Rssi >= medidas[0].Rssi - MargenDb)
            .Select(m => (m.X, m.Y, D: Distancia(m.Rssi, c)))
            .ToList();

        double x, y, radio;
        if (nodos.Count == 1)
        {
            (x, y, radio) = (nodos[0].X, nodos[0].Y, nodos[0].D);
        }
        else if (nodos.Count == 2)
        {
            var (a, b) = (nodos[0], nodos[1]);
            var separacion = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
            var t = a.D / (a.D + b.D);                      // más cerca del que tiene mejor señal (menor distancia)
            (x, y) = (a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
            radio = Math.Abs(separacion - (a.D + b.D)) / 2 + 2.5;   // con 2 nodos no se sabe de qué lado de la línea está
        }
        else
        {
            // Punto de arranque: promedio de los nodos pesado por 1/d² (los cercanos pesan más)
            double sw = 0;
            x = y = 0;
            foreach (var n in nodos) { var w = 1 / (n.D * n.D); x += w * n.X; y += w * n.Y; sw += w; }
            x /= sw; y /= sw;

            // Gauss-Newton: minimiza Σ w·(|p − nodo| − d)²
            for (var i = 0; i < 30; i++)
            {
                double a11 = 0, a12 = 0, a22 = 0, b1 = 0, b2 = 0;
                foreach (var n in nodos)
                {
                    var dx = x - n.X; var dy = y - n.Y;
                    var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1e-6);
                    var (jx, jy) = (dx / dist, dy / dist);
                    var r = dist - n.D;
                    var w = 1 / (n.D * n.D);
                    a11 += w * jx * jx; a12 += w * jx * jy; a22 += w * jy * jy;
                    b1 += w * jx * r; b2 += w * jy * r;
                }
                var det = a11 * a22 - a12 * a12;
                if (Math.Abs(det) < 1e-12) break;           // nodos casi en línea: se queda con el punto de arranque
                var px = (a22 * b1 - a12 * b2) / det;
                var py = (a11 * b2 - a12 * b1) / det;
                x -= px; y -= py;
                if (px * px + py * py < 1e-6) break;
            }

            // Radio: qué tan mal encajan las distancias medidas con el punto encontrado (+1.5 m del ruido del BLE)
            double suma = 0, pesos = 0;
            foreach (var n in nodos)
            {
                var w = 1 / (n.D * n.D);
                var r = Math.Sqrt(Math.Pow(x - n.X, 2) + Math.Pow(y - n.Y, 2)) - n.D;
                suma += w * r * r; pesos += w;
            }
            radio = Math.Sqrt(suma / pesos) + RadioMin;
        }

        // El punto no puede quedar más lejos del nodo más fuerte de lo que dice su señal (+ holgura)
        var fuerte = nodos[0];
        var limite = fuerte.D + HolguraFuerte;
        var lejania = Math.Sqrt(Math.Pow(x - fuerte.X, 2) + Math.Pow(y - fuerte.Y, 2));
        if (lejania > limite)
        {
            x = fuerte.X + (x - fuerte.X) * limite / lejania;
            y = fuerte.Y + (y - fuerte.Y) * limite / lejania;
            radio = Math.Max(radio, limite / 2);
        }

        if (anchoPlano is > 0) x = Math.Clamp(x, 0, anchoPlano.Value);
        if (altoPlano is > 0) y = Math.Clamp(y, 0, altoPlano.Value);
        return new Posicion(Math.Round(x, 1), Math.Round(y, 1), Math.Round(Math.Clamp(radio, RadioMin, RadioMax), 1), nodos.Count);
    }

    // ---------- Ubicación híbrida: por nodo o por triangulación, según la zona ----------

    /// <summary>Lo que hace falta saber de una zona para decidir. El rectángulo es null si no está dibujada.</summary>
    public record ZonaInfo(int Id, string Nombre, ModoUbicacion Modo, bool TieneNodos,
        double? X = null, double? Y = null, double? Ancho = null, double? Alto = null);

    /// <summary>
    /// Dónde está el niño. Metodo: "Nodo" (el nodo de su cuarto la oye mejor), "Triangulacion" (el punto cae en la zona)
    /// o "Cercania" (solo se sabe que está cerca de la zona del nodo más fuerte). Posicion puede venir aunque no se haya usado.
    /// </summary>
    public record Decision(int ZonaId, string Zona, string Metodo, bool Cerca, Posicion? Posicion);

    /// <summary>Un punto con radio mayor a esto (m) no es confiable para decir en qué zona está.</summary>
    private const double RadioConfiable = 6;

    /// <summary>
    /// Decide la zona con las lecturas simultáneas de una pulsera:
    /// 1) si la señal del nodo más fuerte es débil → "Cerca de" su zona (con señales débiles el punto no es confiable);
    /// 2) si ese nodo está en una zona "por nodo" → esa zona;
    /// 3) si no, si el punto triangulado es confiable → la zona donde cae;
    /// 4) si no → la zona del nodo más fuerte.
    /// </summary>
    public static Decision Decidir(IReadOnlyCollection<Lectura> grupo, IReadOnlyDictionary<int, ZonaInfo> zonas,
        Calibracion c, double? anchoPlano = null, double? altoPlano = null)
    {
        var fuerte = MasFuerte(grupo);
        var p = Estimar(grupo, c, anchoPlano, altoPlano);
        if (EsDebil(fuerte))
            return new Decision(fuerte.ZonaId, fuerte.Zona, "Cercania", true, p);

        var modo = zonas.TryGetValue(fuerte.ZonaId, out var zf) ? zf.Modo : ModoUbicacion.PorNodo;
        if (modo == ModoUbicacion.PorNodo)
            return new Decision(fuerte.ZonaId, fuerte.Zona, "Nodo", false, p);

        if (p is not null && p.Radio <= RadioConfiable && ZonaDelPunto(p, zonas.Values) is { } z)
            return new Decision(z.Id, z.Nombre, "Triangulacion", false, p);

        return new Decision(fuerte.ZonaId, fuerte.Zona, "Nodo", false, p);
    }

    /// <summary>
    /// Zona dibujada donde cae el punto. No cuenta los cuartos "por nodo" que tienen nodo propio: si el niño estuviera
    /// adentro, ese nodo la habría oído mejor. Si el punto no cae en ninguna, la más cercana dentro del radio.
    /// </summary>
    public static ZonaInfo? ZonaDelPunto(Posicion p, IEnumerable<ZonaInfo> zonas)
    {
        var candidatas = zonas.Where(z => z.X is not null && !(z.Modo == ModoUbicacion.PorNodo && z.TieneNodos)).ToList();
        double Lejania(ZonaInfo z)
        {
            var dx = Math.Max(Math.Max(z.X!.Value - p.X, 0), p.X - (z.X.Value + z.Ancho!.Value));
            var dy = Math.Max(Math.Max(z.Y!.Value - p.Y, 0), p.Y - (z.Y.Value + z.Alto!.Value));
            return Math.Sqrt(dx * dx + dy * dy);
        }
        // Si cae en dos encimadas, la más chica (la más específica)
        var dentro = candidatas.Where(z => Lejania(z) == 0).MinBy(z => z.Ancho!.Value * z.Alto!.Value);
        if (dentro is not null) return dentro;
        var cercana = candidatas.MinBy(Lejania);
        return cercana is not null && Lejania(cercana) <= p.Radio ? cercana : null;
    }

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
    /// Agrupa lecturas ordenadas por hora en bloques de lecturas simultáneas.
    /// Un bloque empieza con una lectura y abarca las que llegan dentro de <see cref="Simultaneas"/>.
    /// </summary>
    public static List<List<Lectura>> Bloques(IEnumerable<Lectura> lecturasOrdenadas)
    {
        var resultado = new List<List<Lectura>>();
        var bloque = new List<Lectura>();
        foreach (var l in lecturasOrdenadas)
        {
            if (bloque.Count > 0 && l.Hora - bloque[0].Hora > Simultaneas)
            {
                resultado.Add(bloque);
                bloque = [];
            }
            bloque.Add(l);
        }
        if (bloque.Count > 0) resultado.Add(bloque);
        return resultado;
    }

    /// <summary>La lectura más fuerte de cada bloque de lecturas simultáneas.</summary>
    public static List<Lectura> PorMomento(IEnumerable<Lectura> lecturasOrdenadas) =>
        Bloques(lecturasOrdenadas).Select(MasFuerte).ToList();
}
