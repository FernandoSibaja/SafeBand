// ============================================================
// Reglas y piezas compartidas para ver a varios alumnos a la vez: "Mi grupo" (maestro) y
// "Panel de la escuela" (director). Así los dos clasifican y dibujan a los niños igual.
// ============================================================

import { el, hora, haceCuanto } from "./ui.js";

export const MINUTOS_SIN_SENAL = 5;   // los nodos mandan cada 10 s: 5 min sin lecturas = no está cerca de ningún nodo

// Estado de cada alumno, en orden de prioridad (los primeros se listan arriba)
export const ESTADOS = {
    sos:        { orden: 0, texto: "SOS",             clase: "peligro" },
    sinSenal:   { orden: 1, texto: "Sin señal",       clase: "alerta" },
    quitada:    { orden: 2, texto: "Pulsera quitada", clase: "alerta" },
    fuera:      { orden: 3, texto: "Fuera del salón", clase: "azul" },
    nunca:      { orden: 4, texto: "Sin detectar",    clase: "neutra" },
    sinPulsera: { orden: 5, texto: "Sin pulsera",     clase: "neutra" },
    enSalon:    { orden: 6, texto: "En el salón",     clase: "ok" },
};

/** Estados que no se dibujan en el plano (no hay una ubicación actual). */
const SIN_UBICACION = ["sinSenal", "nunca", "sinPulsera"];

/** Estado de un alumno; salonId = zona del salón de su grupo (null si no tiene). */
export function estadoDe(a, salonId) {
    const u = a.ultima;
    if (!a.pulsera) return "sinPulsera";
    if (!u) return "nunca";
    if (u.sos) return "sos";
    if ((Date.now() - new Date(u.timestamp).getTime()) / 60000 > MINUTOS_SIN_SENAL) return "sinSenal";
    if (!u.puesta) return "quitada";
    if (salonId != null && u.zonaId === salonId && !u.senalDebil) return "enSalon";
    return "fuera";
}

/** Agrega `estado` a cada alumno y los ordena: los que requieren atención primero. */
export function clasificar(alumnos, salonDe) {
    return alumnos.map((a) => ({ ...a, estado: estadoDe(a, salonDe(a)) }))
        .sort((x, y) => ESTADOS[x.estado].orden - ESTADOS[y.estado].orden);
}

/** Una cifra del resumen; solo lleva color si hay algo que atender. */
export function cifra(valor, texto, clase) {
    return el("div", { class: clase ? `cifra ${clase}` : "cifra" },
        el("span", { class: "cifra-valor" }, String(valor)),
        el("span", { class: "cifra-texto" }, texto));
}

/** Fila de la tabla de alumnos. conGrupo: agrega la columna "Grupo" (panel de la escuela). */
export function filaAlumno(a, { conGrupo = false } = {}) {
    const u = a.ultima;
    const e = ESTADOS[a.estado];
    return el("tr", {},
        el("td", { class: "principal" }, a.nombre),
        conGrupo && el("td", { "data-etiqueta": "Grupo" }, a.grupo ?? el("span", { class: "secundario" }, "Sin grupo")),
        el("td", { "data-etiqueta": "Dónde" },
            !u ? el("span", { class: "secundario" }, "—")
                : a.estado === "sinSenal" ? `Último: ${u.zona}`
                : u.senalDebil ? `Cerca de ${u.zona}` : u.zona),
        el("td", { "data-etiqueta": "Visto", class: "sin-corte" },
            u ? el("span", { title: hora(u.timestamp) }, haceCuanto(u.timestamp)) : "—"),
        el("td", { "data-etiqueta": "Estado" },
            el("div", { class: "etiquetas" },
                el("span", { class: `etiqueta ${e.clase}` }, e.texto),
                u && !u.puesta && a.estado !== "quitada" && el("span", { class: "etiqueta alerta" }, "Pulsera quitada"),
                u?.bateriaBaja && el("span", { class: "etiqueta alerta" }, "Batería baja"))));
}

/** Encabezado de la tabla de alumnos. */
export function encabezadoTabla({ conGrupo = false } = {}) {
    return el("thead", {}, el("tr", {},
        el("th", { scope: "col" }, "Alumno"),
        conGrupo && el("th", { scope: "col" }, "Grupo"),
        el("th", { scope: "col" }, "Dónde"),
        el("th", { scope: "col" }, "Visto"),
        el("th", { scope: "col" }, "Estado")));
}

/**
 * Marcadores para plano.js: un círculo con iniciales por alumno con ubicación actual.
 * Los triangulados van en su punto; los demás se reparten en una cuadrícula dentro de su zona.
 * agruparDesde: si en una zona hay al menos tantos (no triangulados), se dibuja UN círculo con el número
 * (para una escuela entera). Devuelve { marcadores, mostrados } (mostrados = alumnos que sí aparecen).
 */
export function marcadoresEnPlano(alumnos, zonas, { agruparDesde = Infinity } = {}) {
    const conUbicacion = alumnos.filter((a) => a.ultima && !SIN_UBICACION.includes(a.estado) && zonas.has(a.ultima.zonaId));
    const estiloDe = (a) => (a.estado === "sos" ? "sos" : a.estado === "quitada" ? "antigua" : "actual");
    const marcadores = [];

    const porZona = new Map();
    for (const a of conUbicacion) {
        if (a.ultima.metodo === "Triangulacion" && a.ultima.posicion) {
            marcadores.push({ x: a.ultima.posicion.x, y: a.ultima.posicion.y, texto: iniciales(a.nombre),
                estilo: estiloDe(a), titulo: `${a.nombre} · ${a.ultima.zona}` });
        } else {
            porZona.set(a.ultima.zonaId, [...(porZona.get(a.ultima.zonaId) ?? []), a]);
        }
    }
    for (const [zonaId, enZona] of porZona) {
        const z = zonas.get(zonaId);
        if (enZona.length >= agruparDesde) {
            // Muchos niños: un solo círculo con el número; rojo si alguno tiene SOS
            marcadores.push({ x: z.x + z.ancho / 2, y: z.y + z.alto / 2, texto: String(enZona.length),
                estilo: enZona.some((a) => a.estado === "sos") ? "sos" : "actual",
                titulo: `${z.nombre}: ${enZona.map((a) => a.nombre).join(", ")}`,
                r: Math.min(z.ancho, z.alto) * 0.32, cuenta: true });
            continue;
        }
        const columnas = Math.ceil(Math.sqrt(enZona.length * z.ancho / z.alto));
        const filas = Math.ceil(enZona.length / columnas);
        const r = Math.min(z.ancho / columnas, z.alto / filas) * 0.42;   // que no se enciman entre ellos
        enZona.forEach((a, i) => marcadores.push({
            x: z.x + (z.ancho * ((i % columnas) + 0.5)) / columnas,
            y: z.y + (z.alto * (Math.floor(i / columnas) + 0.5)) / filas,
            r,
            texto: iniciales(a.nombre),
            estilo: estiloDe(a),
            titulo: `${a.nombre} · ${a.ultima.senalDebil ? "cerca de " : ""}${a.ultima.zona}`,
        }));
    }
    return { marcadores, mostrados: conUbicacion.length };
}

// "Fernanda Arellano" → "FA"
export function iniciales(nombre) {
    return nombre.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0].toUpperCase()).join("");
}
