// ============================================================
// Vista "Mi grupo" (rol Maestro) → GET /api/mi-grupo, cada 10 segundos
// Por cada grupo del maestro: resumen (en el salón, fuera, sin señal, pulsera quitada, SOS),
// plano con sus alumnos como puntos y la lista con los que requieren atención primero.
// La API solo devuelve los grupos donde es maestro titular. El plano (GET /api/plano) se pide una vez.
// ============================================================

import { api } from "../api.js";
import { el } from "../ui.js";
import { dibujarPlano } from "../plano.js";
import { clasificar, cifra, filaAlumno, encabezadoTabla, marcadoresEnPlano } from "../estado-alumnos.js";

const INTERVALO_MS = 10000;

export function vistaMiGrupo(contenedor) {
    const indicador = el("span", { class: "en-vivo" }, "Conectando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const lista = el("div", { class: "mis-grupos" }, el("p", { class: "estado" }, "Cargando…"));
    let soloAtencion = false;
    let ultimos = [];

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {},
                el("h1", {}, "Mi grupo"),
                el("p", { class: "sub" }, "Dónde están tus alumnos dentro de la escuela. Se actualiza solo.")),
            indicador),
        aviso,
        el("div", { class: "herramientas" },
            el("label", { class: "casilla" },
                el("input", { type: "checkbox", onchange: (e) => { soloAtencion = e.target.checked; dibujar(ultimos); } }),
                "Mostrar solo a quienes requieren atención")),
        lista);

    let detenido = false;
    let plano = null;

    async function cargar() {
        try {
            const [grupos, planoNuevo] = await Promise.all([
                api("/api/mi-grupo"),
                plano ? plano : api("/api/plano").catch(() => null),
            ]);
            plano = planoNuevo;
            if (detenido) return;
            aviso.hidden = true;
            indicador.classList.remove("desconectado");
            indicador.textContent = `Actualizado ${new Date().toLocaleTimeString("es-MX", { hour: "2-digit", minute: "2-digit" })}`;
            ultimos = grupos;
            dibujar(grupos);
        } catch (e) {
            if (detenido) return;
            aviso.textContent = `${e.message} Se reintentará en unos segundos.`;
            aviso.hidden = false;
            indicador.classList.add("desconectado");
            indicador.textContent = "Sin conexión";
        }
    }

    function dibujar(grupos) {
        if (grupos.length === 0) {
            lista.replaceChildren(el("div", { class: "pendiente" },
                el("strong", {}, "Todavía no tienes grupos. "),
                "La dirección debe asignarte como maestro titular en «Grupos»."));
            return;
        }
        lista.replaceChildren(...grupos.map((g) => tarjetaGrupo(g, plano, soloAtencion)));
    }

    cargar();
    const temporizador = setInterval(cargar, INTERVALO_MS);
    return () => { detenido = true; clearInterval(temporizador); };
}

// ---------- Un grupo ----------

function tarjetaGrupo(g, plano, soloAtencion) {
    const alumnos = clasificar(g.alumnos, () => g.zonaId);
    const cuenta = (e) => alumnos.filter((a) => a.estado === e).length;
    const visibles = soloAtencion ? alumnos.filter((a) => a.estado !== "enSalon") : alumnos;
    const hayAlerta = cuenta("sos") > 0;

    return el("section", { class: hayAlerta ? "grupo-tarjeta sos" : "grupo-tarjeta", "aria-label": `Grupo ${g.nombre}` },
        el("header", { class: "hijo-tarjeta-encabezado" },
            el("h2", {}, `Grupo ${g.nombre}`),
            el("span", { class: "etiqueta neutra" }, g.zona ? `Salón: ${g.zona}` : "Sin salón asignado")),

        hayAlerta && el("div", { class: "alerta-sos", role: "alert" },
            el("strong", {}, "SOS activado: "),
            alumnos.filter((a) => a.estado === "sos").map((a) => a.nombre).join(", "),
            ". La dirección también recibe este aviso."),

        // Resumen: lo primero que ve el maestro
        el("div", { class: "resumen-grupo" },
            g.zonaId != null && cifra(`${cuenta("enSalon")} de ${alumnos.length}`, "en el salón", "ok"),
            cifra(cuenta("fuera"), "fuera del salón", cuenta("fuera") ? "azul" : null),
            cifra(cuenta("sinSenal"), "sin señal", cuenta("sinSenal") ? "alerta" : null),
            cifra(cuenta("quitada"), "pulsera quitada", cuenta("quitada") ? "alerta" : null)),

        planoDelGrupo(g, alumnos, plano),

        alumnos.length === 0
            ? el("p", { class: "secundario" }, "Este grupo todavía no tiene alumnos.")
            : visibles.length === 0
                ? el("p", { class: "secundario" }, "Todos están en el salón.")
                : el("table", { class: "tabla-tarjetas" },
                    encabezadoTabla(),
                    el("tbody", {}, visibles.map((a) => filaAlumno(a)))));
}

// ---------- Plano: un punto por alumno detectado hace poco ----------

function planoDelGrupo(g, alumnos, plano) {
    if (!plano || plano.zonas.length === 0) return null;
    const zonas = new Map(plano.zonas.map((z) => [z.id, z]));
    const { marcadores, mostrados } = marcadoresEnPlano(alumnos, zonas);
    const sinMostrar = alumnos.length - mostrados;

    return el("div", { class: "hijo-plano" },
        el("div", { class: "plano-marco" },
            dibujarPlano(plano.zonas, {
                resaltada: g.zonaId != null && zonas.has(g.zonaId) ? g.zonaId : null,
                estilo: "actual",
                marcadores,
                etiqueta: `Plano de la escuela con ${marcadores.length} alumnos del grupo ${g.nombre}`,
            })),
        sinMostrar > 0 && el("p", { class: "secundario nota" },
            `${sinMostrar} ${sinMostrar === 1 ? "alumno no aparece" : "alumnos no aparecen"} en el plano (sin señal, sin detectar o sin pulsera).`));
}
