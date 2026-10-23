// ============================================================
// Pantalla "Grupos" (solo Administrador)
// - Lista de grupos (3°A...) con su salón, maestro titular y número de alumnos.
// - Formulario en un panel lateral: nombre, salón, maestro y la lista de sus alumnos
//   (casillas con buscador; un alumno que está en otro grupo se cambia a este).
// - Dar de baja (solo sin alumnos activos) y reactivar.
// API: /api/grupos, /api/grupos/maestros, /api/zonas, /api/alumnos
// ============================================================

import { api } from "../api.js";
import { el, icono, avisoBreve, confirmar, campo, crearCajon, normalizar } from "../ui.js";

export function vistaGrupos(contenedor) {
    let grupos = [];
    let incluirInactivos = false;
    let cerrarFormulario = null;

    const conteo = el("p", { class: "sub" }, "Cargando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const cuerpo = el("tbody", {}, filaMensaje("Cargando grupos…"));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {}, el("h1", {}, "Grupos"), conteo),
            el("button", { type: "button", class: "boton boton-primario", onclick: () => abrirFormulario(null) },
                icono("mas", 18), "Agregar grupo")),
        aviso,
        el("div", { class: "herramientas" },
            el("label", { class: "casilla" },
                el("input", { type: "checkbox", onchange: (e) => { incluirInactivos = e.target.checked; cargar(); } }),
                "Mostrar dados de baja")),
        el("div", { class: "tabla-marco" },
            el("table", { class: "tabla-tarjetas" },
                el("thead", {}, el("tr", {},
                    el("th", { scope: "col" }, "Grupo"),
                    el("th", { scope: "col" }, "Salón"),
                    el("th", { scope: "col" }, "Maestro"),
                    el("th", { scope: "col" }, "Alumnos"),
                    el("th", { scope: "col" }, el("span", { class: "oculto-visual" }, "Acciones")))),
                cuerpo)));

    // ---------- Lista ----------

    async function cargar() {
        try {
            grupos = await api(`/api/grupos${incluirInactivos ? "?incluirInactivos=true" : ""}`);
            aviso.hidden = true;
            dibujarTabla();
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
            if (grupos.length === 0) cuerpo.replaceChildren(filaMensaje("No se pudo cargar la lista."));
        }
    }

    function dibujarTabla() {
        const activos = grupos.filter((g) => g.activo).length;
        conteo.textContent = activos === 1 ? "1 grupo activo" : `${activos} grupos activos`;
        cuerpo.replaceChildren(...(grupos.length === 0
            ? [filaMensaje("Todavía no hay grupos. Agrega el primero con «Agregar grupo» (por ejemplo 3°A).")]
            : grupos.map(fila)));
    }

    function fila(g) {
        return el("tr", { class: g.activo ? null : "inactivo" },
            el("td", { class: "principal" },
                g.nombre,
                !g.activo && el("span", { class: "etiqueta neutra", style: "margin-left:8px" }, "Dado de baja")),
            el("td", { "data-etiqueta": "Salón" }, g.zona ?? el("span", { class: "secundario" }, "Sin salón")),
            el("td", { "data-etiqueta": "Maestro" }, g.maestro ?? el("span", { class: "secundario" }, "Sin maestro")),
            el("td", { "data-etiqueta": "Alumnos", class: "num" }, String(g.alumnos.length)),
            el("td", { class: "acciones" },
                el("button", {
                    type: "button", class: "boton boton-enlace", "aria-label": `Editar grupo ${g.nombre}`,
                    onclick: () => abrirFormulario(g),
                }, "Editar")));
    }

    // ---------- Formulario (panel lateral) ----------

    async function abrirFormulario(grupo) {
        cerrarFormulario?.();
        const editando = grupo != null;

        const nombre = campo("g-nombre", "Nombre del grupo",
            el("input", { id: "g-nombre", autocomplete: "off", placeholder: "3°A", value: grupo?.nombre ?? "" }), { ancho: true });
        const selectorZona = el("select", { id: "g-zona" }, el("option", { value: "" }, "Cargando salones…"));
        const zona = campo("g-zona", "Salón", selectorZona,
            { opcional: true, ayuda: "Donde toma clases. Lo usan «Mi grupo» y el pase de lista." });
        const selectorMaestro = el("select", { id: "g-maestro" }, el("option", { value: "" }, "Cargando maestros…"));
        const maestro = campo("g-maestro", "Maestro titular", selectorMaestro, { opcional: true });

        // Alumnos: casillas con buscador
        const buscador = el("input", { type: "search", class: "buscador", placeholder: "Buscar alumno", "aria-label": "Buscar alumno" });
        const contador = el("p", { class: "ayuda-campo" });
        const listaAlumnos = el("div", { class: "lista-casillas", role: "group", "aria-label": "Alumnos del grupo" },
            el("p", { class: "secundario" }, "Cargando alumnos…"));
        const elegidos = new Set((grupo?.alumnos ?? []).map((a) => a.id));
        let alumnos = [];

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Agregar grupo");
        const accionEstado = !editando ? null
            : grupo.activo
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { cerrar } = crearCajon({
            titulo: editando ? `Grupo ${grupo.nombre}` : "Agregar grupo",
            subtitulo: "Su salón, su maestro titular y sus alumnos.",
            contenido: [
                avisoForm,
                el("div", { class: "grupo-campos" }, nombre.nodo, zona.nodo, maestro.nodo),
                el("div", { class: "separador-form" },
                    el("h3", {}, "Alumnos"),
                    el("p", {}, "Marca a los alumnos del grupo. Si alguno está en otro grupo, se cambia a este.")),
                buscador,
                contador,
                listaAlumnos,
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
        nombre.input.focus();
        buscador.addEventListener("input", dibujarAlumnos);

        // Opciones: salones (zonas activas, primero las de tipo Salón), maestros y alumnos activos
        try {
            const [zonas, maestros, todos] = await Promise.all([api("/api/zonas"), api("/api/grupos/maestros"), api("/api/alumnos")]);
            const ordenadas = [...zonas].sort((a, b) => (b.tipo === "Salon") - (a.tipo === "Salon") || a.nombre.localeCompare(b.nombre, "es"));
            selectorZona.replaceChildren(el("option", { value: "" }, "Sin salón"),
                ...ordenadas.map((z) => el("option", { value: z.id }, z.nombre)));
            selectorZona.value = grupo?.zonaId ?? "";

            selectorMaestro.replaceChildren(
                el("option", { value: "" }, maestros.length === 0 ? "Sin maestro (todavía no hay cuentas de maestro)" : "Sin maestro"),
                ...maestros.map((m) => el("option", { value: m.id }, m.email ? `${m.nombre} (${m.email})` : m.nombre)));
            selectorMaestro.value = grupo?.maestroId ?? "";

            alumnos = todos;
            dibujarAlumnos();
        } catch (e) {
            mostrarAviso(`No se pudieron cargar las opciones: ${e.message}`);
        }

        function contar() {
            contador.textContent = elegidos.size === 1 ? "1 alumno marcado" : `${elegidos.size} alumnos marcados`;
        }

        // Solo se vuelve a dibujar al buscar; marcar una casilla actualiza su fila (para no perder el desplazamiento)
        function dibujarAlumnos() {
            contar();
            if (alumnos.length === 0) {
                listaAlumnos.replaceChildren(el("p", { class: "secundario" }, "No hay alumnos activos. Dalos de alta en «Alumnos»."));
                return;
            }
            const texto = normalizar(buscador.value);
            const visibles = alumnos.filter((a) => !texto || normalizar(`${a.nombres} ${a.apellidoPaterno} ${a.apellidoMaterno ?? ""}`).includes(texto));
            listaAlumnos.replaceChildren(...(visibles.length === 0
                ? [el("p", { class: "secundario" }, `Ningún alumno coincide con «${buscador.value}».`)]
                : visibles.map((a) => {
                    const casilla = el("input", { type: "checkbox", checked: elegidos.has(a.id) });
                    const enOtro = a.grupo && a.grupo.id !== grupo?.id;
                    const etiqueta = enOtro ? el("span", { class: "etiqueta neutra" }) : null;
                    const ponerEtiqueta = () => {
                        if (etiqueta) etiqueta.textContent = elegidos.has(a.id) ? `se cambia de ${a.grupo.nombre}` : a.grupo.nombre;
                    };
                    ponerEtiqueta();
                    casilla.addEventListener("change", () => {
                        if (casilla.checked) elegidos.add(a.id); else elegidos.delete(a.id);
                        ponerEtiqueta();
                        contar();
                    });
                    return el("label", { class: "casilla" }, casilla,
                        `${a.apellidoPaterno} ${a.apellidoMaterno ?? ""}, ${a.nombres}`.replace(" ,", ","),
                        etiqueta);
                })));
        }

        async function guardar(evento) {
            evento.preventDefault();
            avisoForm.hidden = true;
            for (const c of [nombre, zona, maestro]) c.limpiar();
            botonGuardar.disabled = true;
            const texto = botonGuardar.textContent;
            botonGuardar.textContent = "Guardando…";
            try {
                const datos = {
                    nombre: nombre.input.value.trim(),
                    zonaId: selectorZona.value === "" ? null : Number(selectorZona.value),
                    maestroId: selectorMaestro.value || null,
                    alumnoIds: [...elegidos],
                };
                if (editando) await api(`/api/grupos/${grupo.id}`, { metodo: "PUT", cuerpo: datos });
                else await api("/api/grupos", { metodo: "POST", cuerpo: datos });
                cerrar();
                avisoBreve(editando ? "Cambios guardados" : "Grupo agregado");
                cargar();
            } catch (e) {
                const errores = e.datos?.errors;
                if (errores?.Nombre || errores?.nombre) nombre.ponerError((errores.Nombre ?? errores.nombre).join(" "));
                else if (/grupo llamado/i.test(e.message)) nombre.ponerError(e.message);
                else if (/zona/i.test(e.message)) zona.ponerError(e.message);
                else if (/maestro/i.test(e.message)) maestro.ponerError(e.message);
                else mostrarAviso(e.message);
            } finally {
                botonGuardar.disabled = false;
                botonGuardar.textContent = texto;
            }
        }

        async function darDeBaja() {
            const ok = await confirmar({
                titulo: `¿Dar de baja el grupo ${grupo.nombre}?`,
                mensaje: "Dejará de aparecer en la lista. No se borra y puedes reactivarlo. Solo se puede si ya no tiene alumnos activos.",
                aceptar: "Dar de baja",
                peligro: true,
            });
            if (!ok) return;
            await accion(() => api(`/api/grupos/${grupo.id}`, { metodo: "DELETE" }), "Grupo dado de baja");
        }

        async function reactivar() {
            await accion(() => api(`/api/grupos/${grupo.id}/reactivar`, { metodo: "POST" }), "Grupo reactivado");
        }

        async function accion(llamada, mensaje) {
            try {
                await llamada();
                cerrar();
                avisoBreve(mensaje);
                cargar();
            } catch (e) {
                mostrarAviso(e.message);
            }
        }

        function mostrarAviso(texto) {
            avisoForm.textContent = texto;
            avisoForm.hidden = false;
        }
    }

    cargar();
    return () => cerrarFormulario?.();
}

function filaMensaje(texto) {
    return el("tr", {}, el("td", { class: "vacio", colspan: 5 }, texto));
}
