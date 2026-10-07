// ============================================================
// Pantalla "Zonas y nodos" (solo Administrador)
// Las zonas se muestran como grupos, cada una con sus nodos adentro.
// - Agregar/editar zona (nombre y tipo) y nodo (código, zona, umbral...).
// - Dar de baja (con confirmación) y reactivar ambos.
// API: /api/zonas, /api/nodos
// ============================================================

import { api } from "../api.js";
import { el, icono, avisoBreve, confirmar, campo, crearCajon, haceCuanto } from "../ui.js";

// Tipos de zona: valor de la API → texto para la persona
const TIPOS = [
    ["Salon", "Salón"],
    ["Patio", "Patio"],
    ["Tienda", "Tienda"],
    ["Entrada", "Entrada"],
    ["Perimetro", "Perímetro (salida de la escuela)"],
    ["Otra", "Otra"],
];
const textoTipo = (valor) => TIPOS.find(([v]) => v === valor)?.[1] ?? valor;

const MINUTOS_EN_LINEA = 2;   // un nodo que mandó datos hace menos de esto se considera "en línea"

export function vistaZonas(contenedor) {
    let zonas = [];
    let nodos = [];
    let incluirInactivos = false;
    let cerrarFormulario = null;

    const conteo = el("p", { class: "sub" }, "Cargando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const lista = el("div", { class: "zonas" }, el("p", { class: "estado" }, "Cargando zonas…"));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {}, el("h1", {}, "Zonas y nodos"), conteo),
            el("div", { style: "display:flex;gap:10px;flex-wrap:wrap" },
                el("button", { type: "button", class: "boton boton-sutil", onclick: () => formularioNodo(null) },
                    icono("mas", 18), "Agregar nodo"),
                el("button", { type: "button", class: "boton boton-primario", onclick: () => formularioZona(null) },
                    icono("mas", 18), "Agregar zona"))),
        aviso,
        el("div", { class: "herramientas" },
            el("label", { class: "casilla" },
                el("input", { type: "checkbox", onchange: (e) => { incluirInactivos = e.target.checked; cargar(); } }),
                "Mostrar dados de baja")),
        lista);

    // ---------- Lista ----------

    async function cargar() {
        const q = incluirInactivos ? "?incluirInactivas=true" : "";
        try {
            [zonas, nodos] = await Promise.all([
                api(`/api/zonas${q}`),
                api(`/api/nodos${incluirInactivos ? "?incluirInactivos=true" : ""}`),
            ]);
            aviso.hidden = true;
            dibujar();
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
            if (zonas.length === 0) lista.replaceChildren(el("p", { class: "estado" }, "No se pudieron cargar las zonas."));
        }
    }

    function dibujar() {
        const zonasActivas = zonas.filter((z) => z.activa).length;
        const nodosActivos = nodos.filter((n) => n.activo).length;
        conteo.textContent = `${zonasActivas} ${zonasActivas === 1 ? "zona" : "zonas"} y ${nodosActivos} ${nodosActivos === 1 ? "nodo activo" : "nodos activos"}`;

        if (zonas.length === 0) {
            lista.replaceChildren(el("div", { class: "pendiente" },
                "Todavía no hay zonas. Empieza con «Agregar zona»: cada salón, el patio, la tienda y la entrada son zonas."));
            return;
        }
        lista.replaceChildren(...zonas.map(tarjetaZona));
    }

    function tarjetaZona(z) {
        const suyos = nodos.filter((n) => n.zonaId === z.id);
        return el("section", { class: z.activa ? "zona" : "zona inactiva", "aria-label": `Zona ${z.nombre}` },
            el("div", { class: "zona-encabezado" },
                el("h2", {}, z.nombre),
                el("span", { class: "etiqueta neutra" }, textoTipo(z.tipo)),
                !z.activa && el("span", { class: "etiqueta neutra" }, "Dada de baja"),
                el("div", { class: "acciones-zona" },
                    z.activa && el("button", {
                        type: "button", class: "boton boton-enlace",
                        onclick: () => formularioNodo(null, z.id),
                    }, "Agregar nodo aquí"),
                    el("button", {
                        type: "button", class: "boton boton-enlace", "aria-label": `Editar zona ${z.nombre}`,
                        onclick: () => formularioZona(z),
                    }, "Editar"))),
            suyos.length === 0
                ? el("div", { class: "zona-vacia" }, "Esta zona todavía no tiene nodos.")
                : el("table", { class: "tabla-tarjetas" },
                    el("thead", {}, el("tr", {},
                        el("th", { scope: "col" }, "Nodo"),
                        el("th", { scope: "col" }, "Descripción"),
                        el("th", { scope: "col" }, "Umbral"),
                        el("th", { scope: "col" }, "Último dato"),
                        el("th", { scope: "col" }, el("span", { class: "oculto-visual" }, "Acciones")))),
                    el("tbody", {}, suyos.map(filaNodo))));
    }

    function filaNodo(n) {
        return el("tr", { class: n.activo ? null : "inactivo" },
            el("td", { class: "principal" },
                n.codigo,
                n.esPortatil && el("span", { class: "etiqueta azul", style: "margin-left:8px" }, "Portátil"),
                !n.activo && el("span", { class: "etiqueta neutra", style: "margin-left:8px" }, "Dado de baja")),
            el("td", { "data-etiqueta": "Descripción" }, n.descripcion ?? "—"),
            el("td", { "data-etiqueta": "Umbral", class: "num" }, `${n.umbralRssi} dBm`),
            el("td", { "data-etiqueta": "Último dato", class: "sin-corte" }, estadoContacto(n)),
            el("td", { class: "acciones" },
                el("button", {
                    type: "button", class: "boton boton-enlace", "aria-label": `Editar nodo ${n.codigo}`,
                    onclick: () => formularioNodo(n),
                }, "Editar")));
    }

    // ---------- Formulario de zona ----------

    function formularioZona(zona) {
        cerrarFormulario?.();
        const editando = zona != null;

        const nombre = campo("z-nombre", "Nombre", el("input", { id: "z-nombre", autocomplete: "off", placeholder: "Salón 3°A", value: zona?.nombre ?? "" }), { ancho: true });
        const selectorTipo = el("select", { id: "z-tipo" }, TIPOS.map(([v, t]) => el("option", { value: v }, t)));
        selectorTipo.value = zona?.tipo ?? "Salon";
        const tipo = campo("z-tipo", "Tipo", selectorTipo, {
            ancho: true,
            ayuda: "El tipo cambia cómo se usa la zona: en los salones se pasa lista y salir del perímetro genera alerta.",
        });

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Agregar zona");
        const accionEstado = !editando ? null
            : zona.activa
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { cerrar } = crearCajon({
            titulo: editando ? zona.nombre : "Agregar zona",
            subtitulo: "Un área de la escuela: un salón, el patio, la tienda, la entrada...",
            contenido: [avisoForm, el("div", { class: "grupo-campos" }, nombre.nodo, tipo.nodo)],
            pie: [accionEstado, botonesPie(() => cerrar(), botonGuardar)],
            alEnviar: guardar,
            alCerrar: () => { cerrarFormulario = null; },
        });
        cerrarFormulario = cerrar;
        nombre.input.focus();

        async function guardar(evento) {
            evento.preventDefault();
            avisoForm.hidden = true;
            nombre.limpiar();
            tipo.limpiar();
            await conBoton(botonGuardar, async () => {
                const datos = { nombre: nombre.input.value.trim(), tipo: selectorTipo.value };
                try {
                    if (editando) await api(`/api/zonas/${zona.id}`, { metodo: "PUT", cuerpo: datos });
                    else await api("/api/zonas", { metodo: "POST", cuerpo: datos });
                    cerrar();
                    avisoBreve(editando ? "Cambios guardados" : "Zona agregada");
                    cargar();
                } catch (e) {
                    repartirErrores(e, { nombre, tipo }, avisoForm, /zona|nombre/i, nombre);
                }
            });
        }

        async function darDeBaja() {
            const ok = await confirmar({
                titulo: `¿Dar de baja la zona ${zona.nombre}?`,
                mensaje: "Dejará de aparecer en la lista. No se borra: sus lecturas se conservan. Solo se puede si ya no tiene nodos activos.",
                aceptar: "Dar de baja",
                peligro: true,
            });
            if (!ok) return;
            await accion(() => api(`/api/zonas/${zona.id}`, { metodo: "DELETE" }), "Zona dada de baja", cerrar, avisoForm);
        }

        async function reactivar() {
            await accion(() => api(`/api/zonas/${zona.id}/reactivar`, { metodo: "POST" }), "Zona reactivada", cerrar, avisoForm);
        }
    }

    // ---------- Formulario de nodo ----------

    function formularioNodo(nodo, zonaInicial = null) {
        cerrarFormulario?.();
        const editando = nodo != null;
        const zonasActivas = zonas.filter((z) => z.activa);

        if (!editando && zonasActivas.length === 0) {
            avisoBreve("Primero agrega una zona");
            formularioZona(null);
            return;
        }

        const codigo = campo("n-codigo", "Código", el("input", { id: "n-codigo", autocomplete: "off", placeholder: "nodo-2", value: nodo?.codigo ?? "" }), {
            ancho: true,
            ayuda: "Debe ser igual al «Código del nodo» que se configuró en el firmware del ESP32 (menuconfig).",
        });
        const selectorZona = el("select", { id: "n-zona" },
            zonasActivas.map((z) => el("option", { value: z.id }, z.nombre)),
            // si el nodo está en una zona dada de baja, se muestra para no perder el dato
            nodo && !zonasActivas.some((z) => z.id === nodo.zonaId) && el("option", { value: nodo.zonaId }, `${nodo.zona} (dada de baja)`));
        selectorZona.value = String(nodo?.zonaId ?? zonaInicial ?? zonasActivas[0]?.id ?? "");
        const zona = campo("n-zona", "Zona", selectorZona, { ancho: true });
        const descripcion = campo("n-descripcion", "Descripción", el("input", {
            id: "n-descripcion", autocomplete: "off", placeholder: "Arriba del pizarrón", value: nodo?.descripcion ?? "",
        }), { opcional: true, ancho: true, ayuda: "Dónde está colocado, para encontrarlo." });
        const umbral = campo("n-umbral", "Umbral de señal (dBm)", el("input", {
            id: "n-umbral", type: "number", min: -127, max: 0, step: 1, value: nodo?.umbralRssi ?? -75,
        }), { ayuda: "Señal mínima para decir que la pulsera está en esta zona. Más cerca de 0 = zona más chica." });
        const portatil = el("input", { type: "checkbox", checked: nodo?.esPortatil ?? false });
        const casillaPortatil = el("label", { class: "casilla ancho" }, portatil, "Nodo portátil (para salidas escolares, con batería)");

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Agregar nodo");
        const accionEstado = !editando ? null
            : nodo.activo
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { cerrar } = crearCajon({
            titulo: editando ? nodo.codigo : "Agregar nodo",
            subtitulo: "Un ESP32 receptor que detecta las pulseras de su zona.",
            contenido: [
                avisoForm,
                el("div", { class: "grupo-campos" }, codigo.nodo, zona.nodo, descripcion.nodo, umbral.nodo, casillaPortatil),
                editando && el("p", { class: "ayuda-campo" },
                    nodo.ultimoContacto ? `Último dato recibido ${haceCuanto(nodo.ultimoContacto)}.` : "Este nodo todavía no ha mandado datos."),
            ],
            pie: [accionEstado, botonesPie(() => cerrar(), botonGuardar)],
            alEnviar: guardar,
            alCerrar: () => { cerrarFormulario = null; },
        });
        cerrarFormulario = cerrar;
        codigo.input.focus();

        async function guardar(evento) {
            evento.preventDefault();
            avisoForm.hidden = true;
            for (const c of [codigo, zona, descripcion, umbral]) c.limpiar();
            await conBoton(botonGuardar, async () => {
                const datos = {
                    codigo: codigo.input.value.trim(),
                    zonaId: selectorZona.value ? Number(selectorZona.value) : null,
                    descripcion: descripcion.input.value.trim() || null,
                    umbralRssi: umbral.input.value === "" ? -75 : Number(umbral.input.value),
                    esPortatil: portatil.checked,
                };
                try {
                    if (editando) await api(`/api/nodos/${nodo.id}`, { metodo: "PUT", cuerpo: datos });
                    else await api("/api/nodos", { metodo: "POST", cuerpo: datos });
                    cerrar();
                    avisoBreve(editando ? "Cambios guardados" : "Nodo agregado");
                    cargar();
                } catch (e) {
                    repartirErrores(e, { codigo, zonaid: zona, descripcion, umbralrssi: umbral }, avisoForm, /c[oó]digo/i, codigo);
                }
            });
        }

        async function darDeBaja() {
            const ok = await confirmar({
                titulo: `¿Dar de baja el nodo ${nodo.codigo}?`,
                mensaje: "La API dejará de aceptar sus lecturas. No se borra: su historial se conserva y puedes reactivarlo.",
                aceptar: "Dar de baja",
                peligro: true,
            });
            if (!ok) return;
            await accion(() => api(`/api/nodos/${nodo.id}`, { metodo: "DELETE" }), "Nodo dado de baja", cerrar, avisoForm);
        }

        async function reactivar() {
            await accion(() => api(`/api/nodos/${nodo.id}/reactivar`, { metodo: "POST" }), "Nodo reactivado", cerrar, avisoForm);
        }
    }

    // ---------- Ayudantes compartidos por los dos formularios ----------

    async function accion(llamada, mensaje, cerrar, avisoForm) {
        try {
            await llamada();
            cerrar();
            avisoBreve(mensaje);
            cargar();
        } catch (e) {
            avisoForm.textContent = e.message;
            avisoForm.hidden = false;
        }
    }

    cargar();
    return () => cerrarFormulario?.();
}

// ---------- Ayudantes de esta pantalla ----------

function botonesPie(alCancelar, botonGuardar) {
    return el("div", { class: "derecha" },
        el("button", { type: "button", class: "boton boton-sutil", onclick: alCancelar }, "Cancelar"),
        botonGuardar);
}

// Deshabilita el botón mientras se guarda y luego lo restaura
async function conBoton(boton, tarea) {
    const texto = boton.textContent;
    boton.disabled = true;
    boton.textContent = "Guardando…";
    try { await tarea(); }
    finally { boton.disabled = false; boton.textContent = texto; }
}

/**
 * Pone cada error de la API junto a su campo. Los errores de reglas ({ error: "..." })
 * que coinciden con `patron` van al `campoReglas`; los demás, al aviso del formulario.
 */
function repartirErrores(e, campos, avisoForm, patron, campoReglas) {
    const errores = e.datos?.errors;
    if (errores) {
        const sinCampo = [];
        for (const [clave, mensajes] of Object.entries(errores)) {
            const c = campos[clave.toLowerCase()];
            if (c) c.ponerError(mensajes.join(" "));
            else sinCampo.push(...mensajes);
        }
        if (sinCampo.length) { avisoForm.textContent = sinCampo.join(" "); avisoForm.hidden = false; }
        return;
    }
    if (e.estado === 409 && patron.test(e.message)) {
        campoReglas.ponerError(e.message);
        campoReglas.input.focus();
    } else {
        avisoForm.textContent = e.message;
        avisoForm.hidden = false;
    }
}

function estadoContacto(n) {
    if (!n.ultimoContacto) return el("span", { class: "secundario" }, "Nunca");
    const minutos = (Date.now() - new Date(n.ultimoContacto).getTime()) / 60000;
    return el("span", {},
        minutos < MINUTOS_EN_LINEA && el("span", { class: "etiqueta ok", style: "margin-right:6px" }, "En línea"),
        haceCuanto(n.ultimoContacto));
}
