// ============================================================
// Conexión con la API. Todas las vistas llaman a la API por aquí.
// - Manda y recibe JSON.
// - La cookie de sesión viaja sola (misma dirección que la API).
// - Convierte cualquier error en un mensaje claro en español.
// ============================================================

export class ErrorApi extends Error {
    constructor(estado, mensaje, datos) {
        super(mensaje);
        this.estado = estado;   // código HTTP (0 = sin conexión)
        this.datos = datos;
    }
}

export async function api(ruta, { metodo = "GET", cuerpo } = {}) {
    let resp;
    try {
        resp = await fetch(ruta, {
            method: metodo,
            headers: cuerpo !== undefined ? { "Content-Type": "application/json" } : {},
            body: cuerpo !== undefined ? JSON.stringify(cuerpo) : undefined,
            credentials: "same-origin",
        });
    } catch {
        throw new ErrorApi(0, "No hay conexión con el servidor. Revisa que la API esté encendida.");
    }

    if (resp.status === 204) return null;

    const texto = await resp.text();
    let datos = null;
    if (texto) {
        try { datos = JSON.parse(texto); } catch { datos = texto; }
    }

    if (!resp.ok) {
        // Sesión vencida en cualquier pantalla (excepto el propio login) → avisar a la app
        if (resp.status === 401 && !ruta.startsWith("/api/auth/")) {
            window.dispatchEvent(new CustomEvent("sesion-vencida"));
        }
        throw new ErrorApi(resp.status, mensajeDeError(resp.status, datos), datos);
    }
    return datos;
}

// La API responde errores de dos formas: { error: "..." } o { errors: { Campo: ["..."] } }
function mensajeDeError(estado, datos) {
    if (datos?.error) return datos.error;
    if (datos?.errors) return Object.values(datos.errors).flat().join(" ");
    if (estado === 401) return "Tu sesión terminó. Inicia sesión de nuevo.";
    if (estado === 403) return "Tu cuenta no tiene permiso para hacer esto.";
    if (estado === 404) return "No se encontró lo que buscas.";
    return `El servidor respondió con un error (${estado}).`;
}
