// ============================================================
// Pantalla "Alumnos" (solo Administrador)
// - Lista con búsqueda y opción de ver los dados de baja.
// - Formulario en un panel lateral para agregar o editar,
//   incluida la pulsera del alumno (libre existente o una nueva).
// - Dar de baja (con confirmación) y reactivar.
// API: /api/alumnos, /api/pulseras?sinAsignar=true, POST /api/pulseras
// ============================================================

import { api } from "../api.js";
import { el, icono, avisoBreve, confirmar, edad, campo, crearCajon, normalizar } from "../ui.js";

const OPCION_NUEVA = "nueva";   // valor del selector para "Registrar una pulsera nueva"

export function vistaAlumnos(contenedor) {
    let alumnos = [];
    let incluirInactivos = false;
    let filtro = "";
    let cerrarFormulario = null;   // si el panel lateral está abierto, esta función lo cierra

    const conteo = el("p", { class: "sub" }, "Cargando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const cuerpo = el("tbody", {}, filaMensaje("Cargando alumnos…"));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {}, el("h1", {}, "Alumnos"), conteo),
            el("button", { type: "button", class: "boton boton-primario", onclick: () => abrirFormulario(null) },
                icono("mas", 18), "Agregar alumno")),
        aviso,
        el("div", { class: "herramientas" },
            el("input", {
                type: "search", class: "buscador", placeholder: "Buscar por nombre o matrícula",
                "aria-label": "Buscar alumno",
                oninput: (e) => { filtro = e.target.value; dibujarTabla(); },
            }),
            el("label", { class: "casilla" },
                el("input", { type: "checkbox", onchange: (e) => { incluirInactivos = e.target.checked; cargar(); } }),
                "Mostrar dados de baja")),
        el("div", { class: "tabla-marco" },
            el("table", { class: "tabla-tarjetas" },
                el("thead", {}, el("tr", {},
                    el("th", { scope: "col" }, "Nombre"),
                    el("th", { scope: "col" }, "Matrícula"),
                    el("th", { scope: "col" }, "Edad"),
                    el("th", { scope: "col" }, "Pulsera"),
                    el("th", { scope: "col" }, el("span", { class: "oculto-visual" }, "Acciones")))),
                cuerpo)));

    // ---------- Lista ----------

    async function cargar() {
        try {
            alumnos = await api(`/api/alumnos${incluirInactivos ? "?incluirInactivos=true" : ""}`);
            aviso.hidden = true;
            dibujarTabla();
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
            if (alumnos.length === 0) cuerpo.replaceChildren(filaMensaje("No se pudo cargar la lista."));
        }
    }

    function dibujarTabla() {
        const activos = alumnos.filter((a) => a.activo).length;
        conteo.textContent = activos === 1 ? "1 alumno activo" : `${activos} alumnos activos`;

        if (alumnos.length === 0) {
            cuerpo.replaceChildren(filaMensaje("Todavía no hay alumnos. Agrega el primero con «Agregar alumno»."));
            return;
        }
        const texto = normalizar(filtro);
        const visibles = alumnos.filter((a) => !texto ||
            normalizar(`${a.nombres} ${a.apellidoPaterno} ${a.apellidoMaterno ?? ""} ${a.matricula ?? ""}`).includes(texto));
        if (visibles.length === 0) {
            cuerpo.replaceChildren(filaMensaje(`Ningún alumno coincide con «${filtro}».`));
            return;
        }
        cuerpo.replaceChildren(...visibles.map(fila));
    }

    function fila(a) {
        const anios = edad(a.fechaNacimiento);
        return el("tr", { class: a.activo ? null : "inactivo" },
            el("td", { class: "principal" },
                nombreCompleto(a),
                !a.activo && el("span", { class: "etiqueta neutra", style: "margin-left:8px" }, "Dado de baja")),
            el("td", { "data-etiqueta": "Matrícula" }, a.matricula ?? "—"),
            el("td", { "data-etiqueta": "Edad", class: "num" }, anios == null ? "—" : `${anios} años`),
            el("td", { "data-etiqueta": "Pulsera" },
                a.pulsera
                    ? el("span", { class: "etiqueta azul" }, a.pulsera.identificadorBle)
                    : el("span", { class: "secundario" }, "Sin pulsera")),
            el("td", { class: "acciones" },
                el("button", {
                    type: "button", class: "boton boton-enlace",
                    "aria-label": `Editar a ${a.nombres} ${a.apellidoPaterno}`,
                    onclick: () => abrirFormulario(a),
                }, "Editar")));
    }

    // ---------- Formulario (panel lateral) ----------

    async function abrirFormulario(alumno) {
        cerrarFormulario?.();
        const editando = alumno != null;

        // Campos
        const nombres = campo("f-nombres", "Nombres", el("input", { id: "f-nombres", autocomplete: "off", value: alumno?.nombres ?? "" }), { ancho: true });
        const paterno = campo("f-paterno", "Apellido paterno", el("input", { id: "f-paterno", autocomplete: "off", value: alumno?.apellidoPaterno ?? "" }));
        const materno = campo("f-materno", "Apellido materno", el("input", { id: "f-materno", autocomplete: "off", value: alumno?.apellidoMaterno ?? "" }), { opcional: true });
        const nacimiento = campo("f-nacimiento", "Fecha de nacimiento",
            el("input", { id: "f-nacimiento", type: "date", max: new Date().toISOString().slice(0, 10), value: alumno?.fechaNacimiento ?? "" }), { opcional: true });
        const matricula = campo("f-matricula", "Matrícula", el("input", { id: "f-matricula", autocomplete: "off", value: alumno?.matricula ?? "" }),
            { opcional: true, ayuda: "El número que la escuela le asigna al alumno." });

        const selector = el("select", { id: "f-pulsera" }, el("option", { value: "" }, "Cargando pulseras…"));
        const pulsera = campo("f-pulsera", "Pulsera", selector, { ancho: true });
        const inputNueva = el("input", { id: "f-nueva", autocomplete: "off", placeholder: "SB-0002" });
        const nueva = campo("f-nueva", "Identificador de la pulsera nueva", inputNueva,
            { ancho: true, ayuda: "El nombre que anuncia por Bluetooth, configurado en su firmware (ej. SB-0002)." });
        nueva.nodo.hidden = true;
        selector.addEventListener("change", () => { nueva.nodo.hidden = selector.value !== OPCION_NUEVA; });

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Agregar alumno");

        const accionEstado = !editando ? null
            : alumno.activo
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { formulario, cerrar } = crearCajon({
            titulo: editando ? `${alumno.nombres} ${alumno.apellidoPaterno}` : "Agregar alumno",
            subtitulo: editando ? "Edita sus datos o cambia su pulsera." : "Sus datos y, si ya la tiene, su pulsera.",
            contenido: [
                avisoForm,
                el("div", { class: "grupo-campos" }, nombres.nodo, paterno.nodo, materno.nodo, nacimiento.nodo, matricula.nodo),
                el("div", { class: "separador-form" },
                    el("h3", {}, "Pulsera"),
                    el("p", {}, "Solo aparecen las pulseras que no tiene otro alumno.")),
                el("div", { class: "grupo-campos" }, pulsera.nodo, nueva.nodo),
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

        // Opciones del selector de pulsera
        let libres = [];
        try {
            libres = await api("/api/pulseras?sinAsignar=true");
        } catch (e) {
            mostrarAvisoForm(`No se pudieron cargar las pulseras libres: ${e.message}`);
        }
        llenarSelector(alumno?.pulsera?.id ?? "");

        function llenarSelector(valor) {
            const opciones = [el("option", { value: "" }, "Sin pulsera")];
            if (alumno?.pulsera) opciones.push(el("option", { value: alumno.pulsera.id }, `${alumno.pulsera.identificadorBle} (la que tiene ahora)`));
            for (const p of libres) opciones.push(el("option", { value: p.id }, p.identificadorBle));
            opciones.push(el("option", { value: OPCION_NUEVA }, "Registrar una pulsera nueva…"));
            selector.replaceChildren(...opciones);
            selector.value = String(valor);
            nueva.nodo.hidden = selector.value !== OPCION_NUEVA;
        }

        // ---------- Guardar ----------

        async function guardar(evento) {
            evento.preventDefault();
            limpiarErrores();
            botonGuardar.disabled = true;
            botonGuardar.textContent = "Guardando…";
            try {
                let pulseraId = selector.value === "" ? null : Number(selector.value);

                // Pulsera nueva: primero se registra, luego se asigna con el alumno
                if (selector.value === OPCION_NUEVA) {
                    if (!inputNueva.value.trim()) {
                        nueva.ponerError("Escribe el identificador de la pulsera (ej. SB-0002).");
                        inputNueva.focus();
                        return;
                    }
                    const creada = await api("/api/pulseras", { metodo: "POST", cuerpo: { identificadorBle: inputNueva.value.trim() } });
                    // Si después falla el alumno, la pulsera ya existe: se deja seleccionada para no registrarla dos veces
                    libres.push(creada);
                    llenarSelector(creada.id);
                    inputNueva.value = "";
                    pulseraId = creada.id;
                }

                const datos = {
                    nombres: nombres.input.value.trim(),
                    apellidoPaterno: paterno.input.value.trim(),
                    apellidoMaterno: materno.input.value.trim() || null,
                    fechaNacimiento: nacimiento.input.value || null,
                    matricula: matricula.input.value.trim() || null,
                    pulseraId,
                };
                if (editando) await api(`/api/alumnos/${alumno.id}`, { metodo: "PUT", cuerpo: datos });
                else await api("/api/alumnos", { metodo: "POST", cuerpo: datos });

                cerrar();
                avisoBreve(editando ? "Cambios guardados" : "Alumno agregado");
                cargar();
            } catch (e) {
                mostrarErrores(e);
            } finally {
                botonGuardar.disabled = false;
                botonGuardar.textContent = editando ? "Guardar cambios" : "Agregar alumno";
            }
        }

        async function darDeBaja() {
            const extra = alumno.pulsera
                ? ` Su pulsera ${alumno.pulsera.identificadorBle} seguirá a su nombre; si se la vas a dar a otro niño, quítasela antes.`
                : "";
            const ok = await confirmar({
                titulo: `¿Dar de baja a ${alumno.nombres} ${alumno.apellidoPaterno}?`,
                mensaje: `Dejará de aparecer en la lista. No se borra: su historial se conserva y puedes reactivarlo cuando quieras.${extra}`,
                aceptar: "Dar de baja",
                peligro: true,
            });
            if (!ok) return;
            try {
                await api(`/api/alumnos/${alumno.id}`, { metodo: "DELETE" });
                cerrar();
                avisoBreve("Alumno dado de baja");
                cargar();
            } catch (e) {
                mostrarAvisoForm(e.message);
            }
        }

        async function reactivar() {
            try {
                await api(`/api/alumnos/${alumno.id}/reactivar`, { metodo: "POST" });
                cerrar();
                avisoBreve("Alumno reactivado");
                cargar();
            } catch (e) {
                mostrarAvisoForm(e.message);
            }
        }

        // ---------- Errores ----------

        // La API responde { errors: { Nombres: [...] } } (validación) o { error: "..." } (reglas)
        const camposPorNombre = {
            nombres, apellidopaterno: paterno, apellidomaterno: materno,
            fechanacimiento: nacimiento, matricula, pulseraid: pulsera, identificadorble: nueva,
        };

        function mostrarErrores(e) {
            const errores = e.datos?.errors;
            if (errores) {
                let sinCampo = [];
                for (const [clave, mensajes] of Object.entries(errores)) {
                    const c = camposPorNombre[clave.toLowerCase()];
                    if (c) c.ponerError(mensajes.join(" "));
                    else sinCampo.push(...mensajes);
                }
                if (sinCampo.length) mostrarAvisoForm(sinCampo.join(" "));
                formulario.querySelector(".con-error input, .con-error select")?.focus();
                return;
            }
            if (/matr[ií]cula/i.test(e.message)) {
                matricula.ponerError(e.message);
                matricula.input.focus();
            } else if (/pulsera/i.test(e.message)) {
                (selector.value === OPCION_NUEVA ? nueva : pulsera).ponerError(e.message);
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
            for (const c of [nombres, paterno, materno, nacimiento, matricula, pulsera, nueva]) c.limpiar();
        }
    }

    cargar();

    // Al salir de la pantalla, cerrar el panel si quedó abierto
    return () => cerrarFormulario?.();
}

// ---------- Ayudantes de esta pantalla ----------

function filaMensaje(texto) {
    return el("tr", {}, el("td", { class: "vacio", colspan: 5 }, texto));
}

function nombreCompleto(a) {
    const apellidos = [a.apellidoPaterno, a.apellidoMaterno].filter(Boolean).join(" ");
    return `${apellidos}, ${a.nombres}`;
}
