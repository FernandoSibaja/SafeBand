using System.ComponentModel.DataAnnotations;
using SafeBand.api.Models;

namespace SafeBand.api.Contracts;

// ---------- Zonas ----------

/// <summary>
/// JSON para dar de alta o editar una zona. Ejemplo:
/// { "nombre": "Patio", "tipo": "Patio" }
/// Tipos: Salon, Patio, Tienda, Entrada, Perimetro, Otra
/// </summary>
public record GuardarZonaRequest(
    [property: Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [property: StringLength(100, ErrorMessage = "El campo 'nombre' admite máximo 100 caracteres.")]
    string? Nombre,

    [property: Required(ErrorMessage = "El campo 'tipo' es obligatorio (Salon, Patio, Tienda, Entrada, Perimetro u Otra).")]
    TipoZona? Tipo);

public record ZonaResponse(
    int Id,
    string Nombre,
    TipoZona Tipo,
    bool Activa,
    int NodosActivos);

// ---------- Nodos ----------

/// <summary>
/// JSON para dar de alta o editar un nodo. Ejemplo:
/// { "codigo": "nodo-2", "zonaId": 2, "descripcion": "Junto a la cancha", "umbralRssi": -75, "esPortatil": false }
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
    int UmbralRssi = -75,

    bool EsPortatil = false);

public record NodoResponse(
    int Id,
    string Codigo,
    string? Descripcion,
    int ZonaId,
    string Zona,
    int UmbralRssi,
    bool EsPortatil,
    bool Activo,
    DateTime? UltimoContacto);
