// ============================================================
// Ayudantes de interfaz que usan todas las vistas.
// ============================================================

/**
 * Crea un elemento HTML. Los textos se insertan con textContent,
 * así ningún dato que venga de la API se interpreta como código.
 *   el("p", { class: "nota" }, "Hola")
 *   el("button", { type: "button", onclick: () => ... }, "Guardar")
 */
export function el(etiqueta, props = {}, ...hijos) {
    const e = document.createElement(etiqueta);
    for (const [clave, valor] of Object.entries(props ?? {})) {
        if (valor == null || valor === false) continue;
        if (clave === "class") e.className = valor;
        else if (clave.startsWith("on") && typeof valor === "function") e.addEventListener(clave.slice(2), valor);
        else e.setAttribute(clave, valor === true ? "" : valor);
    }
    for (const h of hijos.flat()) {
        if (h == null || h === false) continue;
        e.append(h instanceof Node ? h : document.createTextNode(String(h)));
    }
    return e;
}

// ---------- Fechas (la API manda UTC; se muestran en hora local) ----------

export const fechaHora = (iso) =>
    new Date(iso).toLocaleString("es-MX", { dateStyle: "short", timeStyle: "medium" });

export const hora = (iso) =>
    new Date(iso).toLocaleTimeString("es-MX", { hour: "2-digit", minute: "2-digit", second: "2-digit" });

// Si es de hoy, solo la hora; si no, también el día ("30/09 4:19:23 p.m.")
export function cuando(iso) {
    const f = new Date(iso);
    if (f.toDateString() === new Date().toDateString()) return hora(iso);
    return `${f.toLocaleDateString("es-MX", { day: "2-digit", month: "2-digit" })} ${hora(iso)}`;
}

export const esReciente = (iso) => Date.now() - new Date(iso).getTime() < 24 * 3600 * 1000;

export function haceCuanto(iso) {
    const s = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
    if (s < 5) return "justo ahora";
    if (s < 60) return `hace ${s} s`;
    const m = Math.round(s / 60);
    if (m < 60) return `hace ${m} min`;
    const h = Math.round(m / 60);
    if (h < 24) return `hace ${h} h`;
    return fechaHora(iso);
}

// ---------- Señal (RSSI) ----------

// 4 = muy cerca del nodo ... 0 = fuera de alcance. Referencia aproximada hasta calibrar.
export function nivelSenal(rssi) {
    if (rssi >= -60) return 4;
    if (rssi >= -70) return 3;
    if (rssi >= -80) return 2;
    if (rssi >= -90) return 1;
    return 0;
}

const TEXTO_NIVEL = ["Fuera de alcance", "Muy lejos del nodo", "Lejos del nodo", "Cerca del nodo", "Muy cerca del nodo"];
export const textoSenal = (rssi) => TEXTO_NIVEL[nivelSenal(rssi)];

export function barrasSenal(rssi, grande = false) {
    const n = nivelSenal(rssi);
    return el("span",
        { class: grande ? "senal grande" : "senal", "data-nivel": n, role: "img", "aria-label": `${textoSenal(rssi)} (${rssi} dBm)` },
        el("i"), el("i"), el("i"), el("i"));
}

// ---------- Estado de la pulsera ----------

export function etiquetasEstado(l) {
    return el("div", { class: "etiquetas" },
        l.sos && el("span", { class: "etiqueta peligro" }, "SOS"),
        l.puesta
            ? el("span", { class: "etiqueta ok" }, "Puesta")
            : el("span", { class: "etiqueta alerta" }, "Quitada"),
        l.bateriaBaja && el("span", { class: "etiqueta alerta" }, "Batería baja"));
}

// ---------- Aviso breve y confirmación ----------

/** Aviso que aparece abajo unos segundos, ej. "Alumno agregado". */
export function avisoBreve(texto) {
    document.querySelector(".aviso-breve")?.remove();
    const aviso = el("div", { class: "aviso-breve", role: "status" }, texto);
    document.body.append(aviso);
    setTimeout(() => aviso.remove(), 3000);
}

/**
 * Pregunta antes de una acción importante. Devuelve true si la persona confirma.
 *   if (await confirmar({ titulo, mensaje, aceptar: "Dar de baja", peligro: true })) ...
 */
export function confirmar({ titulo, mensaje, aceptar = "Aceptar", peligro = false }) {
    return new Promise((resolver) => {
        const dialogo = el("dialog", { class: "confirmar", "aria-labelledby": "confirmar-titulo" },
            el("div", { class: "contenido-dialogo" },
                el("h2", { id: "confirmar-titulo" }, titulo),
                el("p", {}, mensaje)),
            el("div", { class: "botones" },
                el("button", { type: "button", class: "boton boton-sutil", onclick: () => cerrar(false) }, "Cancelar"),
                el("button", { type: "button", class: peligro ? "boton boton-peligro" : "boton boton-primario", onclick: () => cerrar(true) }, aceptar)));
        function cerrar(valor) { dialogo.close(); dialogo.remove(); resolver(valor); }
        dialogo.addEventListener("cancel", (e) => { e.preventDefault(); cerrar(false); });   // tecla Esc
        document.body.append(dialogo);
        dialogo.showModal();
    });
}

// ---------- Formularios ----------

/** Campo de formulario: etiqueta + control + ayuda + espacio para su error. */
export function campo(id, etiqueta, input, { opcional = false, ayuda = null, ancho = false } = {}) {
    const error = el("p", { class: "error-campo", id: `${id}-error`, hidden: true });
    const nodo = el("div", { class: ancho ? "campo ancho" : "campo" },
        el("label", { for: id }, etiqueta, opcional && el("span", { class: "opcional" }, " (opcional)")),
        input,
        ayuda && el("p", { class: "ayuda-campo" }, ayuda),
        error);
    return {
        nodo,
        input,
        ponerError(texto) {
            error.textContent = texto;
            error.hidden = false;
            nodo.classList.add("con-error");
            input.setAttribute("aria-invalid", "true");
            input.setAttribute("aria-describedby", error.id);
        },
        limpiar() {
            error.hidden = true;
            nodo.classList.remove("con-error");
            input.removeAttribute("aria-invalid");
            input.removeAttribute("aria-describedby");
        },
    };
}

/**
 * Panel lateral con un formulario. Se cierra con Esc, la X, tocando fuera o llamando cerrar().
 * Al cerrarse devuelve el foco a donde estaba.
 */
export function crearCajon({ titulo, subtitulo, contenido, pie, alEnviar, alCerrar }) {
    const focoAnterior = document.activeElement;
    const formulario = el("form", {
        class: "cajon", role: "dialog", "aria-modal": "true", "aria-labelledby": "cajon-titulo",
        novalidate: true, onsubmit: alEnviar,
    },
        el("div", { class: "cajon-encabezado" },
            el("div", {}, el("h2", { id: "cajon-titulo" }, titulo), subtitulo && el("p", { class: "sub" }, subtitulo)),
            el("button", { type: "button", class: "boton boton-enlace", "aria-label": "Cerrar", onclick: () => cerrar() }, icono("cerrar"))),
        el("div", { class: "cajon-cuerpo" }, contenido),
        el("div", { class: "cajon-pie" }, pie));
    const fondo = el("div", { class: "cajon-fondo", onclick: () => cerrar() });
    const alTeclear = (e) => { if (e.key === "Escape" && !document.querySelector("dialog[open]")) cerrar(); };

    document.addEventListener("keydown", alTeclear);
    document.body.append(fondo, formulario);

    function cerrar() {
        document.removeEventListener("keydown", alTeclear);
        fondo.remove();
        formulario.remove();
        alCerrar?.();
        focoAnterior?.focus?.();
    }
    return { formulario, cerrar };
}

/** Para buscar sin importar mayúsculas ni acentos ("lopez" encuentra "López"). */
export function normalizar(texto) {
    return (texto ?? "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();
}

/** Edad en años a partir de "AAAA-MM-DD". */
export function edad(fecha) {
    if (!fecha) return null;
    const [a, m, d] = fecha.split("-").map(Number);
    const hoy = new Date();
    let anios = hoy.getFullYear() - a;
    if (hoy.getMonth() + 1 < m || (hoy.getMonth() + 1 === m && hoy.getDate() < d)) anios--;
    return anios;
}

// ---------- Íconos (trazos simples, heredan el color del texto) ----------

const ICONOS = {
    vivo: '<path d="M3 12h4l2-6 4 12 2-6h6"/>',
    alumnos: '<circle cx="12" cy="8" r="3.5"/><path d="M5 20c.8-3.6 3.6-5.5 7-5.5s6.2 1.9 7 5.5"/>',
    pulseras: '<circle cx="12" cy="12" r="7"/><circle cx="12" cy="12" r="2.5"/>',
    tutores: '<circle cx="8.5" cy="8.5" r="3"/><circle cx="16.5" cy="10" r="2.5"/><path d="M3 19c.6-3 2.8-4.6 5.5-4.6S13.4 16 14 19M14.5 15c2.6-.4 5 .9 5.8 4"/>',
    zonas: '<path d="M4 5h7v6H4zM13 5h7v14h-7zM4 13h7v6H4z"/>',
    menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
    salir: '<path d="M14 5h4a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1h-4M10 8l-4 4 4 4M6 12h9"/>',
    mas: '<path d="M12 5v14M5 12h14"/>',
    cerrar: '<path d="M6 6l12 12M18 6 6 18"/>',
};

export function icono(nombre, tam = 20) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("width", tam);
    svg.setAttribute("height", tam);
    svg.setAttribute("fill", "none");
    svg.setAttribute("stroke", "currentColor");
    svg.setAttribute("stroke-width", "1.8");
    svg.setAttribute("stroke-linecap", "round");
    svg.setAttribute("stroke-linejoin", "round");
    svg.setAttribute("aria-hidden", "true");
    svg.innerHTML = ICONOS[nombre] ?? "";   // contenido fijo del código, nunca datos de la API
    return svg;
}

// Logo: una pulsera anunciándose en ondas
export function logo(tam = 28) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("viewBox", "0 0 32 32");
    svg.setAttribute("width", tam);
    svg.setAttribute("height", tam);
    svg.setAttribute("aria-hidden", "true");
    svg.innerHTML =
        '<circle cx="16" cy="16" r="5" fill="#1D5FC2"/>' +
        '<circle cx="16" cy="16" r="9.5" fill="none" stroke="#1D5FC2" stroke-width="2" opacity=".55"/>' +
        '<circle cx="16" cy="16" r="14" fill="none" stroke="#1D5FC2" stroke-width="2" opacity=".25"/>';
    return svg;
}
