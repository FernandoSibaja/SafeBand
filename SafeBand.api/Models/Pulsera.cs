namespace SafeBand.api.Models;

/// <summary>Pulsera que usa el niño. Se identifica por BLE (ubicación) y por NFC (tienda).</summary>
public class Pulsera
{
    public int Id { get; set; }

    /// <summary>
    /// ID que la pulsera anuncia por BLE, ej. "SB-0001".
    /// No se usa la dirección MAC porque los celulares la cambian aleatoriamente.
    /// </summary>
    public string IdentificadorBle { get; set; } = string.Empty;

    /// <summary>UID del sticker NFC (NTAG215) pegado en la pulsera; se usa en la tienda.</summary>
    public string? UidNfc { get; set; }

    // Relación: a qué alumno pertenece (opcional: una pulsera nueva puede no estar asignada aún)
    public int? AlumnoId { get; set; }
    public Alumno? Alumno { get; set; }

    public bool Activa { get; set; } = true;
    public DateTime FechaAlta { get; set; }

    /// <summary>Última vez que algún nodo la detectó.</summary>
    public DateTime? UltimaLectura { get; set; }
}
