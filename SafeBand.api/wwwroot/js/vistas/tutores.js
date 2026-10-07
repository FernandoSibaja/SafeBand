// ============================================================
// Pantalla "Tutores" (solo Administrador)
// - Lista con búsqueda y opción de ver los dados de baja.
// - Formulario en panel lateral con los datos del tutor y SUS HIJOS:
//   parentesco, contacto principal y si puede recogerlo.
//   Los hijos son lo que decide qué alumnos verá el padre en la app.
// - Dar de baja (con confirmación) y reactivar.
// API: /api/tutores (con "hijos" en el mismo guardado), /api/alumnos
// ============================================================

import { api } from "../api.js";
import { el, icono, avisoBreve, confirmar, campo, crearCajon, normalizar } from "../ui.js";

const PARENTESCOS = ["Madre", "Padre", "Abuela", "Abuelo", "Tía", "Tío", "Tutor legal"];

export function vistaTutores(contenedor) {
    let tutores = [];
    let incluirInactivos = false;
    let filtro = "";
    let cerrarFormulario = null;

    const conteo = el("p", { class: "sub" }, "Cargando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const cuerpo = el("tbody", {}, filaMensaje("Cargando tutores…"));

    // Sugerencias para el campo de parentesco (se puede escribir otro)
    const listaParentescos = el("datalist", { id: "parentescos" }, PARENTESCOS.map((p) => el("option", { value: p })));

    contenedor.append(
        listaParentescos,
        el("header", { class: "encabezado" },
            el("div", {}, el("h1", {}, "Tutores"), conteo),
            el("button", { type: "button", class: "boton boton-primario", onclick: () => abrirFormulario(null) },
                icono("mas", 18), "Agregar tutor")),
        aviso,
        el("div", { class: "herramientas" },
            el("input", {
                type: "search", class: "buscador", placeholder: "Buscar por nombre o correo",
                "aria-label": "Buscar tutor",
                oninput: (e) => { filtro = e.target.value; dibujarTabla(); },
            }),
            el("label", { class: "casilla" },
                el("input", { type: "checkbox", onchange: (e) => { incluirInactivos = e.target.checked; cargar(); } }),
                "Mostrar dados de baja")),
        el("div", { class: "tabla-marco" },
            el("table", { class: "tabla-tarjetas" },
                el("thead", {}, el("tr", {},
                    el("th", { scope: "col" }, "Nombre"),
                    el("th", { scope: "col" }, "Correo"),
                    el("th", { scope: "col" }, "Teléfono"),
                    el("th", { scope: "col" }, "Hijos"),
                    el("th", { scope: "col" }, el("span", { class: "oculto-visual" }, "Acciones")))),
                cuerpo)));

    // ---------- Lista ----------

    async function cargar() {
        try {
            tutores = await api(`/api/tutores${incluirInactivos ? "?incluirInactivos=true" : ""}`);
            aviso.hidden = true;
            dibujarTabla();
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
            if (tutores.length === 0) cuerpo.replaceChildren(filaMensaje("No se pudo cargar la lista."));
        }
    }

    function dibujarTabla() {
        const activos = tutores.filter((t) => t.activo).length;
        conteo.textContent = activos === 1 ? "1 tutor activo" : `${activos} tutores activos`;

        if (tutores.length === 0) {
            cuerpo.replaceChildren(filaMensaje("Todavía no hay tutores. Agrega el primero con «Agregar tutor»."));
            return;
        }
        const texto = normalizar(filtro);
        const visibles = tutores.filter((t) => !texto ||
            normalizar(`${t.nombres} ${t.apellidoPaterno} ${t.apellidoMaterno ?? ""} ${t.email}`).includes(texto));
        if (visibles.length === 0) {
            cuerpo.replaceChildren(filaMensaje(`Ningún tutor coincide con «${filtro}».`));
            return;
        }
        cuerpo.replaceChildren(...visibles.map(fila));
    }

    function fila(t) {
        return el("tr", { class: t.activo ? null : "inactivo" },
            el("td", { class: "principal" },
                nombreCompleto(t),
                !t.activo && el("span", { class: "etiqueta neutra", style: "margin-left:8px" }, "Dado de baja")),
            el("td", { "data-etiqueta": "Correo" }, t.email),
            el("td", { "data-etiqueta": "Teléfono" }, t.telefono ?? "—"),
            el("td", { "data-etiqueta": "Hijos" },
                t.hijos.length
                    ? el("div", { class: "etiquetas" }, t.hijos.map((h) => el("span", { class: "etiqueta azul" }, h.nombre)))
                    : el("span", { class: "secundario" }, "Sin hijos vinculados")),
            el("td", { class: "acciones" },
                el("button", {
                    type: "button", class: "boton boton-enlace",
                    "aria-label": `Editar a ${t.nombres} ${t.apellidoPaterno}`,
                    onclick: () => abrirFormulario(t),
                }, "Editar")));
    }

    // ---------- Formulario (panel lateral) ----------

    async function abrirFormulario(tutor) {
        cerrarFormulario?.();
        const editando = tutor != null;

        // Datos del tutor
        const nombres = campo("t-nombres", "Nombres", el("input", { id: "t-nombres", autocomplete: "off", value: tutor?.nombres ?? "" }), { ancho: true });
        const paterno = campo("t-paterno", "Apellido paterno", el("input", { id: "t-paterno", autocomplete: "off", value: tutor?.apellidoPaterno ?? "" }));
        const materno = campo("t-materno", "Apellido materno", el("input", { id: "t-materno", autocomplete: "off", value: tutor?.apellidoMaterno ?? "" }), { opcional: true });
        const email = campo("t-email", "Correo", el("input", { id: "t-email", type: "email", autocomplete: "off", value: tutor?.email ?? "" }),
            { ancho: true, ayuda: "Con este correo creará su cuenta en la app." });
        const telefono = campo("t-telefono", "Teléfono", el("input", { id: "t-telefono", type: "tel", autocomplete: "off", value: tutor?.telefono ?? "" }),
            { opcional: true, ancho: true });

        // Hijos: se editan en memoria y se mandan todos juntos al guardar
        const hijos = (tutor?.hijos ?? []).map((h) => ({ ...h }));
        const listaHijos = el("div", { class: "hijos" });
        const selectorAlumno = el("select", { id: "t-agregar-hijo", "aria-label": "Alumno para agregar como hijo" });
        const errorHijos = el("p", { class: "error-campo", hidden: true });
        let alumnosActivos = [];

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Agregar tutor");
        const accionEstado = !editando ? null
            : tutor.activo
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { formulario, cerrar } = crearCajon({
            titulo: editando ? `${tutor.nombres} ${tutor.apellidoPaterno}` : "Agregar tutor",
            subtitulo: editando ? "Edita sus datos o qué alumnos puede ver." : "Sus datos y los alumnos de los que es tutor.",
            contenido: [
                avisoForm,
                el("div", { class: "grupo-campos" }, nombres.nodo, paterno.nodo, materno.nodo, email.nodo, telefono.nodo),
                el("div", { class: "separador-form" },
                    el("h3", {}, "Hijos"),
                    el("p", {}, "Solo verá en la app a los alumnos que agregues aquí.")),
                listaHijos,
                el("div", { class: "agregar-hijo" },
                    selectorAlumno,
                    el("button", { type: "button", class: "boton boton-sutil", onclick: agregarHijo }, icono("mas", 18), "Agregar")),
                errorHijos,
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
        nombres.input.focus();

        dibujarHijos();
        try {
            alumnosActivos = await api("/api/alumnos");
        } catch (e) {
            mostrarAvisoForm(`No se pudo cargar la lista de alumnos: ${e.message}`);
        }
        llenarSelector();

        // ---------- Hijos ----------

        function dibujarHijos() {
            if (hijos.length === 0) {
                listaHijos.replaceChildren(el("p", { class: "hijos-vacio" },
                    "Sin hijos vinculados. Mientras no tenga, este tutor no verá a ningún alumno."));
                return;
            }
            listaHijos.replaceChildren(...hijos.map((h) => {
                const idBase = `hijo-${h.alumnoId}`;
                return el("div", { class: "hijo" },
                    el("span", { class: "hijo-nombre" }, h.nombre),
                    el("button", {
                        type: "button", class: "boton boton-enlace", "aria-label": `Quitar a ${h.nombre}`,
                        onclick: () => quitarHijo(h.alumnoId),
                    }, "Quitar"),
                    el("div", { class: "hijo-opciones" },
                        el("label", { class: "oculto-visual", for: `${idBase}-par` }, `Parentesco con ${h.nombre}`),
                        el("input", {
                            id: `${idBase}-par`, type: "text", list: "parentescos", placeholder: "Parentesco",
                            value: h.parentesco ?? "", oninput: (e) => { h.parentesco = e.target.value; },
                        }),
                        el("label", { class: "casilla" },
                            el("input", { type: "checkbox", checked: h.esContactoPrincipal, onchange: (e) => { h.esContactoPrincipal = e.target.checked; } }),
                            "Contacto principal"),
                        el("label", { class: "casilla" },
                            el("input", { type: "checkbox", checked: h.puedeRecoger, onchange: (e) => { h.puedeRecoger = e.target.checked; } }),
                            "Puede recogerlo")));
            }));
        }

        function llenarSelector() {
            const disponibles = alumnosActivos.filter((a) => !hijos.some((h) => h.alumnoId === a.id));
            selectorAlumno.replaceChildren(
                el("option", { value: "" }, disponibles.length ? "Elegir alumno…" : "No hay más alumnos para agregar"),
                ...disponibles.map((a) => el("option", { value: a.id }, `${a.apellidoPaterno} ${a.apellidoMaterno ?? ""}, ${a.nombres}`.replace(/\s+,/, ","))));
        }

        function agregarHijo() {
            errorHijos.hidden = true;
            const id = Number(selectorAlumno.value);
            const alumno = alumnosActivos.find((a) => a.id === id);
            if (!alumno) {
                errorHijos.textContent = "Elige un alumno de la lista.";
                errorHijos.hidden = false;
                selectorAlumno.focus();
                return;
            }
            hijos.push({
                alumnoId: alumno.id,
                nombre: `${alumno.nombres} ${alumno.apellidoPaterno}`,
                parentesco: "",
                esContactoPrincipal: false,
                puedeRecoger: true,
            });
            dibujarHijos();
            llenarSelector();
            document.getElementById(`hijo-${alumno.id}-par`)?.focus();
        }

        function quitarHijo(alumnoId) {
            const i = hijos.findIndex((h) => h.alumnoId === alumnoId);
            if (i >= 0) hijos.splice(i, 1);
            dibujarHijos();
            llenarSelector();
            selectorAlumno.focus();
        }

        // ---------- Guardar, baja, reactivar ----------

        async function guardar(evento) {
            evento.preventDefault();
            limpiarErrores();
            botonGuardar.disabled = true;
            botonGuardar.textContent = "Guardando…";
            try {
                const datos = {
                    nombres: nombres.input.value.trim(),
                    apellidoPaterno: paterno.input.value.trim(),
                    apellidoMaterno: materno.input.value.trim() || null,
                    email: email.input.value.trim(),
                    telefono: telefono.input.value.trim() || null,
                    hijos: hijos.map((h) => ({
                        alumnoId: h.alumnoId,
                        parentesco: (h.parentesco ?? "").trim() || null,
                        esContactoPrincipal: h.esContactoPrincipal,
                        puedeRecoger: h.puedeRecoger,
                    })),
                };
                if (editando) await api(`/api/tutores/${tutor.id}`, { metodo: "PUT", cuerpo: datos });
                else await api("/api/tutores", { metodo: "POST", cuerpo: datos });

                cerrar();
                avisoBreve(editando ? "Cambios guardados" : "Tutor agregado");
                cargar();
            } catch (e) {
                mostrarErrores(e);
            } finally {
                botonGuardar.disabled = false;
                botonGuardar.textContent = editando ? "Guardar cambios" : "Agregar tutor";
            }
        }

        async function darDeBaja() {
            const ok = await confirmar({
                titulo: `¿Dar de baja a ${tutor.nombres} ${tutor.apellidoPaterno}?`,
                mensaje: "Dejará de aparecer en la lista. No se borra: sus datos y sus hijos vinculados se conservan, y puedes reactivarlo cuando quieras.",
                aceptar: "Dar de baja",
                peligro: true,
            });
            if (!ok) return;
            try {
                await api(`/api/tutores/${tutor.id}`, { metodo: "DELETE" });
                cerrar();
                avisoBreve("Tutor dado de baja");
                cargar();
            } catch (e) {
                mostrarAvisoForm(e.message);
            }
        }

        async function reactivar() {
            try {
                await api(`/api/tutores/${tutor.id}/reactivar`, { metodo: "POST" });
                cerrar();
                avisoBreve("Tutor reactivado");
                cargar();
            } catch (e) {
                mostrarAvisoForm(e.message);
            }
        }

        // ---------- Errores ----------

        const camposPorNombre = { nombres, apellidopaterno: paterno, apellidomaterno: materno, email, telefono };

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
                formulario.querySelector(".con-error input")?.focus();
                return;
            }
            if (/correo/i.test(e.message)) {
                email.ponerError(e.message);
                email.input.focus();
            } else {
                mostrarAvisoForm(e.message);
            }
        }

        function mostrarAvisoForm(texto) {
            avisoForm.textContent = texto;
            avisoForm.hidden = false;
        }

        function limpiarErrores() {
            avisoForm.hidden = true;
            errorHijos.hidden = true;
            for (const c of [nombres, paterno, materno, email, telefono]) c.limpiar();
        }
    }

    cargar();
    return () => cerrarFormulario?.();
}

function filaMensaje(texto) {
    return el("tr", {}, el("td", { class: "vacio", colspan: 5 }, texto));
}

function nombreCompleto(t) {
    const apellidos = [t.apellidoPaterno, t.apellidoMaterno].filter(Boolean).join(" ");
    return `${apellidos}, ${t.nombres}`;
}
