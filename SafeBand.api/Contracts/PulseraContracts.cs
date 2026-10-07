using System.ComponentModel.DataAnnotations;

namespace SafeBand.api.Contracts;

/// <summary>
/// JSON para dar de alta o editar una pulsera (y a qué alumno pertenece, en la misma operación). Ejemplo:
/// { "identificadorBle": "SB-0002", "uidNfc": "04A1B2C3D4E5F6", "alumnoId": 3 }
/// alumnoId = null → la pulsera queda libre.
/// </summary>
public record GuardarPulseraRequest(
    // Debe empezar con "SB-": los nodos solo escuchan anuncios con ese prefijo.
    // Máximo 15 caracteres en total (límite del firmware del nodo).
    [property: Required(ErrorMessage = "El campo 'identificadorBle' es obligatorio.")]
    [property: RegularExpression(@"^[Ss][Bb]-[A-Za-z0-9]{1,12}$",
        ErrorMessage = "El campo 'identificadorBle' debe tener el formato SB-XXXX (letras o números, máximo 15 caracteres).")]
    string? IdentificadorBle,

    // UID del sticker NFC en hexadecimal; se aceptan separadores (04:A1:B2...) y se quitan.
    [property: StringLength(40, ErrorMessage = "El campo 'uidNfc' es demasiado largo.")]
    string? UidNfc = null,

    int? AlumnoId = null);

/// <summary>JSON para asignar (o quitar) la pulsera a un alumno. alumnoId = null la deja sin asignar.</summary>
public record AsignarPulseraRequest(int? AlumnoId);

/// <summary>JSON que devuelve la API al consultar una pulsera.</summary>
public record PulseraResponse(
    int Id,
    string IdentificadorBle,
    string? UidNfc,
    bool Activa,
    DateTime FechaAlta,
    DateTime? UltimaLectura,
    int? AlumnoId,
    string? Alumno);   // nombre completo del alumno, si está asignada
