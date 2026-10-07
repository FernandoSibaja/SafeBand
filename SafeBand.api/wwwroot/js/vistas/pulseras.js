// ============================================================
// Pantalla "Pulseras" (solo Administrador) — el inventario de pulseras
// - Lista con búsqueda y filtro Todas / Asignadas / Libres.
// - "Última señal": detecta pulseras que no han reportado (batería, daño, no la traen).
// - Registrar / editar (identificador BLE, UID NFC y alumno en la misma operación).
// - Dar de baja (perdida o dañada: queda libre) y reactivar.
// API: /api/pulseras, /api/alumnos
// ============================================================

import { api } from "../api.js";
import { el, icono, avisoBreve, confirmar, campo, crearCajon, normalizar, haceCuanto } from "../ui.js";

const HORAS_SIN_SENAL = 24;   // una pulsera asignada sin señal en más de esto se marca para revisar

export function vistaPulseras(contenedor) {
    let pulseras = [];
    let incluirInactivas = false;
    let filtro = "";
    let mostrar = "todas";   // todas | asignadas | libres
    let cerrarFormulario = null;

    const conteo = el("p", { class: "sub" }, "Cargando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const cuerpo = el("tbody", {}, filaMensaje("Cargando pulseras…"));

    const segmento = (valor, texto) => el("label", {},
        el("input", {
            type: "radio", name: "mostrar", value: valor, checked: valor === mostrar,
            onchange: () => { mostrar = valor; dibujarTabla(); },
        }), texto);

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {}, el("h1", {}, "Pulseras"), conteo),
            el("button", { type: "button", class: "boton boton-primario", onclick: () => abrirFormulario(null) },
                icono("mas", 18), "Registrar pulsera")),
        aviso,
        el("div", { class: "herramientas" },
            el("input", {
                type: "search", class: "buscador", placeholder: "Buscar por pulsera, NFC o alumno",
                "aria-label": "Buscar pulsera",
                oninput: (e) => { filtro = e.target.value; dibujarTabla(); },
            }),
            el("fieldset", { class: "segmentos" },
                el("legend", { class: "oculto-visual" }, "Mostrar"),
                segmento("todas", "Todas"), segmento("asignadas", "Asignadas"), segmento("libres", "Libres")),
            el("label", { class: "casilla" },
                el("input", { type: "checkbox", onchange: (e) => { incluirInactivas = e.target.checked; cargar(); } }),
                "Mostrar dadas de baja")),
        el("div", { class: "tabla-marco" },
            el("table", { class: "tabla-tarjetas" },
                el("thead", {}, el("tr", {},
                    el("th", { scope: "col" }, "Pulsera"),
                    el("th", { scope: "col" }, "NFC"),
                    el("th", { scope: "col" }, "Alumno"),
                    el("th", { scope: "col" }, "Última señal"),
                    el("th", { scope: "col" }, el("span", { class: "oculto-visual" }, "Acciones")))),
                cuerpo)));

    // ---------- Lista ----------

    async function cargar() {
        try {
            pulseras = await api(`/api/pulseras${incluirInactivas ? "?incluirInactivas=true" : ""}`);
            aviso.hidden = true;
            dibujarTabla();
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
            if (pulseras.length === 0) cuerpo.replaceChildren(filaMensaje("No se pudo cargar la lista."));
        }
    }

    function dibujarTabla() {
        const activas = pulseras.filter((p) => p.activa);
        const asignadas = activas.filter((p) => p.alumnoId != null).length;
        const libres = activas.length - asignadas;
        conteo.textContent = `${activas.length} ${activas.length === 1 ? "activa" : "activas"}: ` +
            `${asignadas} ${asignadas === 1 ? "asignada" : "asignadas"} y ${libres} ${libres === 1 ? "libre" : "libres"}`;

        if (pulseras.length === 0) {
            cuerpo.replaceChildren(filaMensaje("Todavía no hay pulseras. Registra la primera con «Registrar pulsera»."));
            return;
        }
        const texto = normalizar(filtro);
        const visibles = pulseras.filter((p) =>
            (mostrar === "todas" || (mostrar === "asignadas") === (p.alumnoId != null)) &&
            (!texto || normalizar(`${p.identificadorBle} ${p.uidNfc ?? ""} ${p.alumno ?? ""}`).includes(texto)));
        if (visibles.length === 0) {
            cuerpo.replaceChildren(filaMensaje("Ninguna pulsera coincide con la búsqueda o el filtro."));
            return;
        }
        cuerpo.replaceChildren(...visibles.map(fila));
    }

    function fila(p) {
        return el("tr", { class: p.activa ? null : "inactivo" },
            el("td", { class: "principal sin-corte" },
                p.identificadorBle,
                !p.activa && el("span", { class: "etiqueta neutra", style: "margin-left:8px" }, "Dada de baja")),
            el("td", { "data-etiqueta": "NFC", class: "num" }, p.uidNfc ?? "—"),
            el("td", { "data-etiqueta": "Alumno" },
                p.alumno ?? (p.activa ? el("span", { class: "etiqueta ok" }, "Libre") : "—")),
            el("td", { "data-etiqueta": "Última señal", class: "sin-corte" }, ultimaSenal(p)),
            el("td", { class: "acciones" },
                el("button", {
                    type: "button", class: "boton boton-enlace", "aria-label": `Editar pulsera ${p.identificadorBle}`,
                    onclick: () => abrirFormulario(p),
                }, "Editar")));
    }

    // ---------- Formulario (panel lateral) ----------

    async function abrirFormulario(pulsera) {
        cerrarFormulario?.();
        const editando = pulsera != null;

        const idBle = campo("p-id", "Identificador", el("input", {
            id: "p-id", autocomplete: "off", placeholder: "SB-0004", value: pulsera?.identificadorBle ?? "",
        }), { ancho: true, ayuda: "El nombre que la pulsera anuncia por Bluetooth (formato SB-XXXX), configurado en su firmware." });
        const uid = campo("p-uid", "UID del sticker NFC", el("input", {
            id: "p-uid", autocomplete: "off", placeholder: "04A1B2C3D4E5F6", value: pulsera?.uidNfc ?? "",
        }), { opcional: true, ancho: true, ayuda: "El número del sticker NFC de la pulsera. Lo usará la terminal de la tienda." });
        const selectorAlumno = el("select", { id: "p-alumno" }, el("option", { value: "" }, "Cargando alumnos…"));
        const alumno = campo("p-alumno", "Alumno", selectorAlumno, {
            ancho: true, ayuda: "Solo aparecen los alumnos que todavía no tienen pulsera.",
        });

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Registrar pulsera");
        const accionEstado = !editando ? null
            : pulsera.activa
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { cerrar } = crearCajon({
            titulo: editando ? pulsera.identificadorBle : "Registrar pulsera",
            subtitulo: editando ? "Corrige sus datos o cambia de alumno." : "Da de alta una pulsera y, si ya sabes de quién es, asígnala.",
            contenido: [
                avisoForm,
                el("div", { class: "grupo-campos" }, idBle.nodo, uid.nodo, alumno.nodo),
                editando && el("p", { class: "ayuda-campo" },
                    pulsera.ultimaLectura ? `Última señal recibida ${haceCuanto(pulsera.ultimaLectura)}.` : "Esta pulsera todavía no ha sido detectada por ningún nodo."),
            ],
            pie: [
                accionEstado,
                el("div", { class: "derecha" },
                    el("button", { type: "button", class: "boton boton-sutil", onclick: () => cerrar() }, "Cancelar"),
                    botonGuardar),
            ],
            alEnviar: guardar,
            alCerrar: () => { cerrarFormulario = null; },
        });
        cerrarFormulario = cerrar;
        idBle.input.focus();

        // Opciones de alumno: libre, el actual y los que no tienen pulsera
        try {
            const alumnos = await api("/api/alumnos");
            const opciones = [el("option", { value: "" }, "Libre (sin alumno)")];
            if (pulsera?.alumnoId) opciones.push(el("option", { value: pulsera.alumnoId }, `${pulsera.alumno} (el actual)`));
            for (const a of alumnos.filter((a) => !a.pulsera))
                opciones.push(el("option", { value: a.id, "data-nombre": `${a.nombres} ${a.apellidoPaterno}` },
                    `${a.apellidoPaterno}${a.apellidoMaterno ? " " + a.apellidoMaterno : ""}, ${a.nombres}`));
            selectorAlumno.replaceChildren(...opciones);
            selectorAlumno.value = String(pulsera?.alumnoId ?? "");
            selectorAlumno.disabled = editando && !pulsera.activa;   // una pulsera dada de baja no se asigna
        } catch (e) {
            mostrarAvisoForm(`No se pudo cargar la lista de alumnos: ${e.message}`);
        }

        async function guardar(evento) {
            evento.preventDefault();
            limpiarErrores();
            const alumnoId = selectorAlumno.value ? Number(selectorAlumno.value) : null;

            // Quitarle la pulsera a un niño es delicado: se confirma
            if (editando && pulsera.alumnoId && alumnoId !== pulsera.alumnoId) {
                const ok = await confirmar({
                    titulo: `¿Quitarle la pulsera a ${pulsera.alumno}?`,
                    mensaje: alumnoId
                        ? `La pulsera ${pulsera.identificadorBle} pasará a ser de ${selectorAlumno.selectedOptions[0].dataset.nombre}. ${pulsera.alumno} se quedará sin pulsera.`
                        : `La pulsera ${pulsera.identificadorBle} quedará libre y ${pulsera.alumno} se quedará sin pulsera.`,
                    aceptar: "Sí, cambiar",
                    peligro: true,
                });
                if (!ok) return;
            }

            botonGuardar.disabled = true;
            botonGuardar.textContent = "Guardando…";
            try {
                const datos = { identificadorBle: idBle.input.value.trim(), uidNfc: uid.input.value.trim() || null, alumnoId };
                if (editando) await api(`/api/pulseras/${pulsera.id}`, { metodo: "PUT", cuerpo: datos });
                else await api("/api/pulseras", { metodo: "POST", cuerpo: datos });
                cerrar();
                avisoBreve(editando ? "Cambios guardados" : "Pulsera registrada");
                cargar();
            } catch (e) {
                mostrarErrores(e);
            } finally {
                botonGuardar.disabled = false;
                botonGuardar.textContent = editando ? "Guardar cambios" : "Registrar pulsera";
            }
        }

        async function darDeBaja() {
            const ok = await confirmar({
                titulo: `¿Dar de baja la pulsera ${pulsera.identificadorBle}?`,
                mensaje: (pulsera.alumno ? `${pulsera.alumno} se quedará sin pulsera y podrás darle otra. ` : "") +
                    "La API dejará de aceptar sus lecturas. No se borra: su historial se conserva y puedes reactivarla si aparece.",
                aceptar: "Dar de baja",
                peligro: true,
            });
            if (!ok) return;
            await accion(() => api(`/api/pulseras/${pulsera.id}`, { metodo: "DELETE" }), "Pulsera dada de baja");
        }

        async function reactivar() {
            await accion(() => api(`/api/pulseras/${pulsera.id}/reactivar`, { metodo: "POST" }), "Pulsera reactivada (libre)");
        }

        async function accion(llamada, mensaje) {
            try {
                await llamada();
                cerrar();
                avisoBreve(mensaje);
                cargar();
            } catch (e) {
                mostrarAvisoForm(e.message);
            }
        }

        // ---------- Errores ----------

        const camposPorNombre = { identificadorble: idBle, uidnfc: uid, alumnoid: alumno };

        function mostrarErrores(e) {
            const errores = e.datos?.errors;
            if (errores) {
                const sinCampo = [];
                for (const [clave, mensajes] of Object.entries(errores)) {
                    const c = camposPorNombre[clave.toLowerCase()];
                    if (c) c.ponerError(mensajes.join(" "));
                    else sinCampo.push(...mensajes);
                }
                if (sinCampo.length) mostrarAvisoForm(sinCampo.join(" "));
                return;
            }
            if (/identificador/i.test(e.message)) { idBle.ponerError(e.message); idBle.input.focus(); }
            else if (/uid/i.test(e.message)) { uid.ponerError(e.message); uid.input.focus(); }
            else if (/alumno|ya tiene la pulsera/i.test(e.message)) alumno.ponerError(e.message);
            else mostrarAvisoForm(e.message);
        }

        function mostrarAvisoForm(texto) {
            avisoForm.textContent = texto;
            avisoForm.hidden = false;
        }

        function limpiarErrores() {
            avisoForm.hidden = true;
            for (const c of [idBle, uid, alumno]) c.limpiar();
        }
    }

    cargar();
    return () => cerrarFormulario?.();
}

function filaMensaje(texto) {
    return el("tr", {}, el("td", { class: "vacio", colspan: 5 }, texto));
}

// "hace 2 min", o una etiqueta de alerta si una pulsera asignada no ha dado señal en mucho tiempo
function ultimaSenal(p) {
    const horas = p.ultimaLectura ? (Date.now() - new Date(p.ultimaLectura).getTime()) / 3600000 : Infinity;
    const revisar = p.activa && p.alumnoId != null && horas > HORAS_SIN_SENAL;
    return el("span", {},
        revisar && el("span", { class: "etiqueta alerta", style: "margin-right:6px" }, "Revisar"),
        p.ultimaLectura ? haceCuanto(p.ultimaLectura) : el("span", { class: "secundario" }, "Nunca"));
}
