// ============================================================
// Pantalla "Zonas y nodos" (solo Administrador)
// Las zonas se muestran como grupos, cada una con sus nodos adentro.
// - Arriba, el plano de la escuela con las zonas dibujadas (tocar una la edita).
// - Agregar/editar zona (nombre, tipo y rectángulo en el plano) y nodo (código, zona, umbral...).
// - Dar de baja (con confirmación) y reactivar ambos.
// API: /api/zonas, /api/nodos
// ============================================================

import { api } from "../api.js";
import { el, icono, avisoBreve, confirmar, campo, crearCajon, haceCuanto } from "../ui.js";
import { dibujarPlano, seEnciman } from "../plano.js";

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

// Cómo se ubica a un niño en la zona (igual que Zona.PorDefecto en la API)
const UBICACIONES = { PorNodo: "Por nodo", Triangulacion: "Por triangulación" };
const ubicacionPorTipo = (tipo) => (tipo === "Salon" || tipo === "Tienda" ? "PorNodo" : "Triangulacion");

const MINUTOS_EN_LINEA = 2;   // un nodo que mandó datos hace menos de esto se considera "en línea"

export function vistaZonas(contenedor) {
    let zonas = [];
    let nodos = [];
    let incluirInactivos = false;
    let cerrarFormulario = null;

    const conteo = el("p", { class: "sub" }, "Cargando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const lista = el("div", { class: "zonas" }, el("p", { class: "estado" }, "Cargando zonas…"));
    const tarjetaPlano = el("section", { class: "plano-tarjeta", "aria-label": "Plano de la escuela", hidden: true });

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
        tarjetaPlano,
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
        dibujarTarjetaPlano();
    }

    // Plano con las zonas activas que ya tienen rectángulo; tocar una abre su formulario
    function dibujarTarjetaPlano() {
        const enPlano = zonasEnPlano(zonas.filter((z) => z.activa));
        const sinDibujar = zonas.filter((z) => z.activa && !z.plano).length;
        tarjetaPlano.hidden = false;
        tarjetaPlano.replaceChildren(
            el("div", { class: "plano-encabezado" },
                el("h2", {}, "Plano de la escuela"),
                enPlano.length > 0 && sinDibujar > 0 &&
                    el("span", { class: "secundario" }, `${sinDibujar} ${sinDibujar === 1 ? "zona sin dibujar" : "zonas sin dibujar"}`)),
            enPlano.length === 0
                ? el("p", { class: "secundario" },
                    "Ninguna zona está dibujada todavía. Edita una zona y llena «Ubicación en el plano» con sus medidas en metros; así los padres verán dónde está su hijo.")
                : el("div", { class: "plano-marco" },
                    dibujarPlano(enPlano, {
                        alClic: (z) => formularioZona(zonas.find((x) => x.id === z.id)),
                        nodos: nodosEnPlano(nodos.filter((n) => n.activo)),
                    })));
    }

    function tarjetaZona(z) {
        const suyos = nodos.filter((n) => n.zonaId === z.id);
        return el("section", { class: z.activa ? "zona" : "zona inactiva", "aria-label": `Zona ${z.nombre}` },
            el("div", { class: "zona-encabezado" },
                el("h2", {}, z.nombre),
                el("span", { class: "etiqueta neutra" }, textoTipo(z.tipo)),
                el("span", { class: "etiqueta neutra", title: "Cómo se ubica a un niño en esta zona" }, UBICACIONES[z.ubicacionEfectiva]),
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

        // --- Cómo se ubica (vacío = automático según el tipo) ---
        const selectorUbicacion = el("select", { id: "z-ubicacion" });
        const ubicacion = campo("z-ubicacion", "Cómo se ubica a un niño aquí", selectorUbicacion, {
            ancho: true,
            ayuda: "Por nodo: hay un nodo dentro del cuarto y basta con que sea el que mejor oye la pulsera. "
                + "Por triangulación: espacio abierto o cuarto chico sin nodo; se calcula el punto con los nodos de alrededor.",
        });
        function llenarUbicaciones() {
            const elegido = selectorUbicacion.value;
            selectorUbicacion.replaceChildren(
                el("option", { value: "" }, `Automático (${UBICACIONES[ubicacionPorTipo(selectorTipo.value)].toLowerCase()}, por ser ${textoTipo(selectorTipo.value).toLowerCase()})`),
                ...Object.entries(UBICACIONES).map(([v, t]) => el("option", { value: v }, t)));
            selectorUbicacion.value = elegido;
        }
        llenarUbicaciones();
        selectorUbicacion.value = zona?.ubicacion ?? "";
        selectorTipo.addEventListener("change", llenarUbicaciones);

        // --- Ubicación en el plano (rectángulo en metros) ---
        const enPlano = el("input", { type: "checkbox", checked: zona?.plano != null });
        const casillaPlano = el("label", { class: "casilla ancho" }, enPlano, "Dibujar esta zona en el plano");
        const numero = (id, valor, min) => el("input", { id, type: "number", inputmode: "decimal", min, max: 1000, step: 0.5, value: valor ?? "" });
        const px = campo("z-x", "X (desde la izquierda)", numero("z-x", zona?.plano?.x ?? 0, 0));
        const py = campo("z-y", "Y (desde arriba)", numero("z-y", zona?.plano?.y ?? 0, 0));
        const pancho = campo("z-ancho", "Ancho", numero("z-ancho", zona?.plano?.ancho ?? 8, 0.5));
        const palto = campo("z-alto", "Alto", numero("z-alto", zona?.plano?.alto ?? 6, 0.5));
        const camposPlano = [px, py, pancho, palto];
        const avisoEncime = el("p", { class: "ayuda-campo aviso-encime", hidden: true });
        const vistaPrevia = el("div", { class: "plano-marco plano-previa" });
        const seccionPlano = el("div", { class: "grupo-campos" },
            el("p", { class: "ayuda-campo ancho" },
                "Medidas en metros. (0, 0) es la esquina superior izquierda de la escuela; X crece a la derecha y Y hacia abajo."),
            px.nodo, py.nodo, pancho.nodo, palto.nodo,
            el("div", { class: "ancho" }, vistaPrevia, avisoEncime));

        // Rectángulo que hay en los campos ahora mismo (null si falta algún dato)
        function rectanguloActual() {
            const [x, y, an, al] = camposPlano.map((c) => c.input.value === "" ? NaN : Number(c.input.value));
            return [x, y, an, al].some(Number.isNaN) ? null : { x, y, ancho: an, alto: al };
        }

        // Vista previa: las demás zonas en gris y esta en azul
        function actualizarPrevia() {
            seccionPlano.hidden = !enPlano.checked;
            if (!enPlano.checked) return;
            const otras = zonasEnPlano(zonas.filter((z) => z.activa && z.id !== zona?.id));
            const rect = rectanguloActual();
            const valido = rect && rect.ancho >= 0.5 && rect.alto >= 0.5 && rect.x >= 0 && rect.y >= 0;
            const esta = valido ? [{ id: "esta", nombre: nombre.input.value.trim() || "Esta zona", ...rect }] : [];
            vistaPrevia.replaceChildren(otras.length + esta.length === 0
                ? el("p", { class: "secundario" }, "Escribe las medidas para ver la vista previa.")
                : dibujarPlano([...otras, ...esta], { resaltada: "esta", cuadricula: true, etiqueta: "Vista previa del plano" }));
            // Aviso (no bloquea): se encima con otra zona
            const encimadas = valido ? otras.filter((o) => seEnciman(o, rect)).map((o) => o.nombre) : [];
            avisoEncime.hidden = encimadas.length === 0;
            avisoEncime.textContent = `Se encima con: ${encimadas.join(", ")}. Revisa las medidas si no es intencional.`;
        }
        enPlano.addEventListener("change", actualizarPrevia);
        for (const c of [nombre, ...camposPlano]) c.input.addEventListener("input", actualizarPrevia);

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Agregar zona");
        const accionEstado = !editando ? null
            : zona.activa
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { cerrar } = crearCajon({
            titulo: editando ? zona.nombre : "Agregar zona",
            subtitulo: "Un área de la escuela: un salón, el patio, la tienda, la entrada...",
            contenido: [
                avisoForm,
                el("div", { class: "grupo-campos" }, nombre.nodo, tipo.nodo, ubicacion.nodo),
                el("h3", { class: "subtitulo-form" }, "Ubicación en el plano"),
                casillaPlano,
                seccionPlano,
            ],
            pie: [accionEstado, botonesPie(() => cerrar(), botonGuardar)],
            alEnviar: guardar,
            alCerrar: () => { cerrarFormulario = null; },
        });
        cerrarFormulario = cerrar;
        actualizarPrevia();
        nombre.input.focus();

        async function guardar(evento) {
            evento.preventDefault();
            avisoForm.hidden = true;
            for (const c of [nombre, tipo, ubicacion, ...camposPlano]) c.limpiar();
            await conBoton(botonGuardar, async () => {
                // Campo vacío → null, para que la API diga cuál falta
                const num = (c) => c.input.value === "" ? null : Number(c.input.value);
                const datos = {
                    nombre: nombre.input.value.trim(),
                    tipo: selectorTipo.value,
                    ubicacion: selectorUbicacion.value || null,
                    plano: enPlano.checked ? { x: num(px), y: num(py), ancho: num(pancho), alto: num(palto) } : null,
                };
                try {
                    if (editando) await api(`/api/zonas/${zona.id}`, { metodo: "PUT", cuerpo: datos });
                    else await api("/api/zonas", { metodo: "POST", cuerpo: datos });
                    cerrar();
                    avisoBreve(editando ? "Cambios guardados" : "Zona agregada");
                    cargar();
                } catch (e) {
                    repartirErrores(e, { nombre, tipo, ubicacion, "plano.x": px, "plano.y": py, "plano.ancho": pancho, "plano.alto": palto },
                        avisoForm, /zona|nombre/i, nombre);
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
            id: "n-umbral", type: "number", min: -127, max: 0, step: 1, value: nodo?.umbralRssi ?? -90,
        }), { ayuda: "Señal mínima para decir que la pulsera está en esta zona. Según la prueba de alcance, −90 dBm ≈ 5–6 m. Más cerca de 0 = zona más chica." });
        const portatil = el("input", { type: "checkbox", checked: nodo?.esPortatil ?? false });
        const casillaPortatil = el("label", { class: "casilla ancho" }, portatil, "Nodo portátil (para salidas escolares, con batería)");

        // --- Ubicación en el plano (punto en metros, la usa la triangulación) ---
        const zonasDibujadas = zonasEnPlano(zonas.filter((z) => z.activa));
        const enPlanoN = el("input", { type: "checkbox", checked: nodo?.posicion != null });
        const casillaPlanoN = el("label", { class: "casilla ancho" }, enPlanoN, "Ubicar este nodo en el plano");
        const numeroN = (id, valor) => el("input", { id, type: "number", inputmode: "decimal", min: 0, max: 1000, step: 0.1, value: valor ?? "" });
        const nx = campo("n-x", "X (desde la izquierda)", numeroN("n-x", nodo?.posicion?.x));
        const ny = campo("n-y", "Y (desde arriba)", numeroN("n-y", nodo?.posicion?.y));
        const previaN = el("div", { class: "plano-marco plano-previa" });
        const seccionPlanoN = el("div", { class: "grupo-campos" },
            el("p", { class: "ayuda-campo ancho" },
                "Toca el plano donde está colocado el nodo, o escribe X y Y en metros. Con ±1 m de precisión es suficiente."),
            nx.nodo, ny.nodo,
            el("div", { class: "ancho" }, previaN));

        function puntoActual() {
            const x = nx.input.value === "" ? NaN : Number(nx.input.value);
            const y = ny.input.value === "" ? NaN : Number(ny.input.value);
            return Number.isNaN(x) || Number.isNaN(y) ? null : { x, y };
        }
        function actualizarPreviaN() {
            seccionPlanoN.hidden = !enPlanoN.checked;
            if (!enPlanoN.checked) return;
            if (zonasDibujadas.length === 0) {
                previaN.replaceChildren(el("p", { class: "secundario" }, "Primero dibuja las zonas en el plano (Editar zona → Ubicación en el plano)."));
                return;
            }
            const punto = puntoActual();
            const otros = nodosEnPlano(nodos.filter((n) => n.activo && n.id !== nodo?.id));
            const este = punto ? [{ ...punto, etiqueta: codigo.input.value.trim() || "Este nodo", actual: true }] : [];
            previaN.replaceChildren(dibujarPlano(zonasDibujadas, {
                resaltada: Number(selectorZona.value),
                cuadricula: true,
                nodos: [...otros, ...este],
                alClicPunto: (x, y) => { nx.input.value = x; ny.input.value = y; actualizarPreviaN(); },
                etiqueta: "Plano para ubicar el nodo",
            }));
        }
        enPlanoN.addEventListener("change", actualizarPreviaN);
        selectorZona.addEventListener("change", actualizarPreviaN);
        for (const c of [codigo, nx, ny]) c.input.addEventListener("input", actualizarPreviaN);

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
                el("h3", { class: "subtitulo-form" }, "Ubicación en el plano"),
                casillaPlanoN,
                seccionPlanoN,
                editando && el("p", { class: "ayuda-campo" },
                    nodo.ultimoContacto ? `Último dato recibido ${haceCuanto(nodo.ultimoContacto)}.` : "Este nodo todavía no ha mandado datos."),
            ],
            pie: [accionEstado, botonesPie(() => cerrar(), botonGuardar)],
            alEnviar: guardar,
            alCerrar: () => { cerrarFormulario = null; },
        });
        cerrarFormulario = cerrar;
        actualizarPreviaN();
        codigo.input.focus();

        async function guardar(evento) {
            evento.preventDefault();
            avisoForm.hidden = true;
            for (const c of [codigo, zona, descripcion, umbral, nx, ny]) c.limpiar();
            await conBoton(botonGuardar, async () => {
                const datos = {
                    codigo: codigo.input.value.trim(),
                    zonaId: selectorZona.value ? Number(selectorZona.value) : null,
                    descripcion: descripcion.input.value.trim() || null,
                    umbralRssi: umbral.input.value === "" ? -90 : Number(umbral.input.value),
                    esPortatil: portatil.checked,
                    posicion: enPlanoN.checked
                        ? { x: nx.input.value === "" ? null : Number(nx.input.value), y: ny.input.value === "" ? null : Number(ny.input.value) }
                        : null,
                };
                try {
                    if (editando) await api(`/api/nodos/${nodo.id}`, { metodo: "PUT", cuerpo: datos });
                    else await api("/api/nodos", { metodo: "POST", cuerpo: datos });
                    cerrar();
                    avisoBreve(editando ? "Cambios guardados" : "Nodo agregado");
                    cargar();
                } catch (e) {
                    repartirErrores(e, { codigo, zonaid: zona, descripcion, umbralrssi: umbral, "posicion.x": nx, "posicion.y": ny },
                        avisoForm, /c[oó]digo/i, codigo);
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

// Nodos de la API → puntos para plano.js (solo los que tienen posición)
function nodosEnPlano(lista) {
    return lista.filter((n) => n.posicion).map((n) => ({ x: n.posicion.x, y: n.posicion.y, etiqueta: n.codigo }));
}

// Zonas de la API → rectángulos para plano.js (solo las que tienen plano)
function zonasEnPlano(lista) {
    return lista.filter((z) => z.plano).map((z) => ({ id: z.id, nombre: z.nombre, ...z.plano }));
}

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
