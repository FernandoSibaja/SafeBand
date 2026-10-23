// ============================================================
// "Panel de la escuela" (rol Administrador) → GET /api/escuela, cada 10 segundos
// Todos los alumnos: cifras de la escuela, avisos de SOS, buscador ("¿dónde está Kevin?"),
// filtro por grupo, plano con todos los niños (una zona con muchos muestra el número),
// conteo por zona y la lista con los que requieren atención primero.
// El plano (GET /api/plano) se pide una vez.
// ============================================================

import { api } from "../api.js";
import { el, normalizar } from "../ui.js";
import { dibujarPlano } from "../plano.js";
import { clasificar, cifra, filaAlumno, encabezadoTabla, marcadoresEnPlano } from "../estado-alumnos.js";

const INTERVALO_MS = 10000;
const AGRUPAR_DESDE = 8;       // con tantos niños en una zona, el plano muestra el número en lugar de cada uno
const MAX_FILAS = 100;         // la lista se corta aquí (los que requieren atención siempre van primero)
const SIN_GRUPO = "sin-grupo";

export function vistaEscuela(contenedor) {
    const indicador = el("span", { class: "en-vivo" }, "Conectando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const selectorGrupo = el("select", { "aria-label": "Filtrar por grupo", class: "selector-filtro" },
        el("option", { value: "" }, "Todos los grupos"));
    const buscador = el("input", { type: "search", class: "buscador", placeholder: "¿Dónde está…? Escribe un nombre", "aria-label": "Buscar alumno" });
    const casillaAtencion = el("input", { type: "checkbox" });
    const cuerpo = el("div", { class: "panel-escuela" }, el("p", { class: "estado" }, "Cargando…"));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {},
                el("h1", {}, "Panel de la escuela"),
                el("p", { class: "sub" }, "Dónde están todos los alumnos ahora. Se actualiza solo.")),
            indicador),
        aviso,
        el("div", { class: "herramientas" },
            buscador,
            selectorGrupo,
            el("label", { class: "casilla" }, casillaAtencion, "Solo quienes requieren atención")),
        cuerpo);

    let detenido = false;
    let plano = null;
    let alumnos = [];
    for (const control of [buscador, selectorGrupo, casillaAtencion])
        control.addEventListener(control === buscador ? "input" : "change", dibujar);

    async function cargar() {
        try {
            const [lista, planoNuevo] = await Promise.all([
                api("/api/escuela"),
                plano ? plano : api("/api/plano").catch(() => null),
            ]);
            plano = planoNuevo;
            if (detenido) return;
            aviso.hidden = true;
            indicador.classList.remove("desconectado");
            indicador.textContent = `Actualizado ${new Date().toLocaleTimeString("es-MX", { hour: "2-digit", minute: "2-digit" })}`;
            alumnos = lista;
            llenarGrupos();
            dibujar();
        } catch (e) {
            if (detenido) return;
            aviso.textContent = `${e.message} Se reintentará en unos segundos.`;
            aviso.hidden = false;
            indicador.classList.add("desconectado");
            indicador.textContent = "Sin conexión";
        }
    }

    // Opciones del filtro según los grupos que tienen alumnos (conserva lo elegido)
    function llenarGrupos() {
        const elegido = selectorGrupo.value;
        const grupos = [...new Map(alumnos.filter((a) => a.grupoId != null).map((a) => [a.grupoId, a.grupo])).entries()]
            .sort((x, y) => x[1].localeCompare(y[1], "es"));
        selectorGrupo.replaceChildren(
            el("option", { value: "" }, "Todos los grupos"),
            ...grupos.map(([id, nombre]) => el("option", { value: id }, nombre)),
            alumnos.some((a) => a.grupoId == null) && el("option", { value: SIN_GRUPO }, "Sin grupo"));
        selectorGrupo.value = [...selectorGrupo.options].some((o) => o.value === elegido) ? elegido : "";
    }

    function dibujar() {
        if (alumnos.length === 0) {
            cuerpo.replaceChildren(el("div", { class: "pendiente" }, "Todavía no hay alumnos activos. Dalos de alta en «Alumnos»."));
            return;
        }
        // Las cifras y los SOS siempre son de toda la escuela; el filtro solo cambia plano y lista
        const todos = clasificar(alumnos, (a) => a.salonId);
        const cuenta = (e) => todos.filter((a) => a.estado === e).length;
        const conPulsera = todos.filter((a) => a.pulsera).length;
        const ubicados = todos.filter((a) => ["enSalon", "fuera", "quitada", "sos"].includes(a.estado)).length;
        const sos = todos.filter((a) => a.estado === "sos");

        const texto = normalizar(buscador.value.trim());
        const grupo = selectorGrupo.value;
        const filtrados = todos.filter((a) =>
            (!texto || normalizar(a.nombre).includes(texto))
            && (grupo === "" || (grupo === SIN_GRUPO ? a.grupoId == null : String(a.grupoId) === grupo))
            && (!casillaAtencion.checked || a.estado !== "enSalon"));

        cuerpo.replaceChildren(
            sos.length > 0 && el("div", { class: "alerta-sos", role: "alert" },
                el("strong", {}, sos.length === 1 ? "SOS activado: " : `${sos.length} SOS activados: `),
                sos.map((a) => `${a.nombre}${a.grupo ? ` (${a.grupo})` : ""} en ${a.ultima.zona}`).join(" · ")),

            el("div", { class: "resumen-grupo" },
                cifra(`${ubicados} de ${conPulsera}`, "con pulsera, ubicados ahora", "ok"),
                cifra(cuenta("enSalon"), "en su salón"),
                cifra(cuenta("fuera"), "fuera de su salón", cuenta("fuera") ? "azul" : null),
                cifra(cuenta("sinSenal"), "sin señal", cuenta("sinSenal") ? "alerta" : null),
                cifra(cuenta("quitada"), "pulsera quitada", cuenta("quitada") ? "alerta" : null),
                cifra(todos.filter((a) => a.ultima?.bateriaBaja).length, "batería baja",
                    todos.some((a) => a.ultima?.bateriaBaja) ? "alerta" : null)),

            el("div", { class: "panel-escuela-cuerpo" },
                planoEscuela(filtrados, texto !== "" || grupo !== ""),
                conteoPorZona(filtrados)),

            filtrados.length === 0
                ? el("p", { class: "secundario" }, texto ? `Ningún alumno coincide con «${buscador.value.trim()}».` : "No hay alumnos con este filtro.")
                : el("div", { class: "tabla-marco" },
                    el("table", { class: "tabla-tarjetas" },
                        encabezadoTabla({ conGrupo: true }),
                        el("tbody", {}, filtrados.slice(0, MAX_FILAS).map((a) => filaAlumno(a, { conGrupo: true })))),
                    filtrados.length > MAX_FILAS && el("p", { class: "secundario nota" },
                        `Se muestran ${MAX_FILAS} de ${filtrados.length}. Usa el buscador o el filtro de grupo para ver a los demás.`)));
    }

    function planoEscuela(filtrados, hayFiltro) {
        if (!plano || plano.zonas.length === 0) return null;
        const zonas = new Map(plano.zonas.map((z) => [z.id, z]));
        // Con búsqueda o grupo se ve a cada niño; sin filtro, las zonas llenas muestran el número
        const { marcadores, mostrados } = marcadoresEnPlano(filtrados, zonas, { agruparDesde: hayFiltro ? Infinity : AGRUPAR_DESDE });
        // Si la búsqueda dio un solo niño, se resalta su zona
        const unico = filtrados.length === 1 && filtrados[0].ultima && zonas.has(filtrados[0].ultima.zonaId) ? filtrados[0] : null;
        return el("div", { class: "plano-tarjeta" },
            el("div", { class: "plano-marco" },
                dibujarPlano(plano.zonas, {
                    resaltada: unico ? unico.ultima.zonaId : null,
                    estilo: unico?.estado === "sos" ? "sos" : unico?.estado === "sinSenal" ? "antigua" : "actual",
                    marcadores,
                    etiqueta: unico
                        ? `Plano: ${unico.nombre} ${unico.estado === "sinSenal" ? "se detectó por última vez en" : "está en"} ${unico.ultima.zona}`
                        : `Plano de la escuela con ${mostrados} alumnos`,
                })),
            filtrados.length - mostrados > 0 && el("p", { class: "secundario nota" },
                `${filtrados.length - mostrados} sin ubicación actual (sin señal, sin detectar o sin pulsera) no aparecen en el plano.`));
    }

    // Cuántos niños hay en cada zona ahora (solo los que tienen ubicación actual)
    function conteoPorZona(filtrados) {
        const cuenta = new Map();
        for (const a of filtrados) {
            if (!a.ultima || !["enSalon", "fuera", "quitada", "sos"].includes(a.estado)) continue;
            cuenta.set(a.ultima.zona, (cuenta.get(a.ultima.zona) ?? 0) + 1);
        }
        const filas = [...cuenta.entries()].sort((x, y) => y[1] - x[1] || x[0].localeCompare(y[0], "es"));
        return el("section", { class: "conteo-zonas", "aria-label": "Alumnos por zona" },
            el("h2", {}, "Por zona"),
            filas.length === 0
                ? el("p", { class: "secundario" }, "Nadie ubicado ahora.")
                : el("ol", {}, filas.map(([zona, n]) => el("li", {}, el("span", {}, zona), el("strong", {}, String(n))))));
    }

    cargar();
    const temporizador = setInterval(cargar, INTERVALO_MS);
    return () => { detenido = true; clearInterval(temporizador); };
}
