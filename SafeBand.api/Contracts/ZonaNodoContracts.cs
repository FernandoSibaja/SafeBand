using System.ComponentModel.DataAnnotations;
using SafeBand.api.Models;

namespace SafeBand.api.Contracts;

// ---------- Zonas ----------

/// <summary>
/// JSON para dar de alta o editar una zona. Ejemplo:
/// { "nombre": "Patio", "tipo": "Patio", "plano": { "x": 0, "y": 6, "ancho": 15, "alto": 8 } }
/// Tipos: Salon, Patio, Tienda, Entrada, Perimetro, Otra
/// "plano" es opcional (null = la zona no se dibuja en el plano).
/// </summary>
public record GuardarZonaRequest(
    [property: Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [property: StringLength(100, ErrorMessage = "El campo 'nombre' admite máximo 100 caracteres.")]
    string? Nombre,

    [property: Required(ErrorMessage = "El campo 'tipo' es obligatorio (Salon, Patio, Tienda, Entrada, Perimetro u Otra).")]
    TipoZona? Tipo,

    RectanguloPlano? Plano = null,

    ModoUbicacion? Ubicacion = null);   // PorNodo, Triangulacion o null (automático según el tipo)

/// <summary>
/// Rectángulo de una zona en el plano de la escuela, en metros.
/// (x, y) = esquina superior izquierda; x crece a la derecha, y hacia abajo.
/// </summary>
public record RectanguloPlano(
    [property: Required(ErrorMessage = "El campo 'plano.x' es obligatorio.")]
    [property: Range(0, 1000, ErrorMessage = "El campo 'plano.x' debe estar entre 0 y 1000 metros.")]
    double? X,

    [property: Required(ErrorMessage = "El campo 'plano.y' es obligatorio.")]
    [property: Range(0, 1000, ErrorMessage = "El campo 'plano.y' debe estar entre 0 y 1000 metros.")]
    double? Y,

    [property: Required(ErrorMessage = "El campo 'plano.ancho' es obligatorio.")]
    [property: Range(0.5, 1000, ErrorMessage = "El campo 'plano.ancho' debe estar entre 0.5 y 1000 metros.")]
    double? Ancho,

    [property: Required(ErrorMessage = "El campo 'plano.alto' es obligatorio.")]
    [property: Range(0.5, 1000, ErrorMessage = "El campo 'plano.alto' debe estar entre 0.5 y 1000 metros.")]
    double? Alto);

public record ZonaResponse(
    int Id,
    string Nombre,
    TipoZona Tipo,
    bool Activa,
    int NodosActivos,
    RectanguloPlano? Plano,
    ModoUbicacion? Ubicacion,           // lo que eligió el administrador (null = automático)
    ModoUbicacion UbicacionEfectiva);   // el que se usa de verdad

/// <summary>Plano de la escuela: tamaño total (en metros) y las zonas activas que tienen rectángulo.</summary>
public record PlanoResponse(
    double Ancho,
    double Alto,
    List<ZonaEnPlano> Zonas);

public record ZonaEnPlano(
    int Id,
    string Nombre,
    TipoZona Tipo,
    double X,
    double Y,
    double Ancho,
    double Alto,
    ModoUbicacion Ubicacion);

// ---------- Nodos ----------

/// <summary>
/// JSON para dar de alta o editar un nodo. Ejemplo:
/// { "codigo": "nodo-2", "zonaId": 2, "descripcion": "Junto a la cancha", "umbralRssi": -90, "esPortatil": false,
///   "posicion": { "x": 12.5, "y": 3.0 } }
/// "posicion" es opcional (dónde está el nodo en el plano, en metros; la usa la triangulación).
/// </summary>
public record GuardarNodoRequest(
    // Debe coincidir con el "Código del nodo" configurado en el firmware (menuconfig)
    [property: Required(ErrorMessage = "El campo 'codigo' es obligatorio.")]
    [property: RegularExpression(@"^[A-Za-z0-9_\-]{1,50}$",
        ErrorMessage = "El campo 'codigo' solo admite letras, números, guion y guion bajo (máximo 50).")]
    string? Codigo,

    [property: Required(ErrorMessage = "El campo 'zonaId' es obligatorio.")]
    int? ZonaId,

    [property: StringLength(200, ErrorMessage = "El campo 'descripcion' admite máximo 200 caracteres.")]
    string? Descripcion = null,

    [property: Range(-127, 0, ErrorMessage = "El campo 'umbralRssi' debe estar entre -127 y 0 dBm.")]
    int UmbralRssi = -90,

    bool EsPortatil = false,

    PosicionPlano? Posicion = null);

/// <summary>Un punto en el plano de la escuela, en metros.</summary>
public record PosicionPlano(
    [property: Required(ErrorMessage = "El campo 'posicion.x' es obligatorio.")]
    [property: Range(0, 1000, ErrorMessage = "El campo 'posicion.x' debe estar entre 0 y 1000 metros.")]
    double? X,

    [property: Required(ErrorMessage = "El campo 'posicion.y' es obligatorio.")]
    [property: Range(0, 1000, ErrorMessage = "El campo 'posicion.y' debe estar entre 0 y 1000 metros.")]
    double? Y);

public record NodoResponse(
    int Id,
    string Codigo,
    string? Descripcion,
    int ZonaId,
    string Zona,
    int UmbralRssi,
    bool EsPortatil,
    bool Activo,
    DateTime? UltimoContacto,
    PosicionPlano? Posicion);
