// ============================================================
// Pantalla "Alertas" (Administrador y Maestro) → GET /api/alertas, cada 10 segundos
// - Arriba, las ACTIVAS en rojo con su botón "Desactivar" (solo si a esta cuenta le toca:
//   el director siempre; el maestro, si es titular del grupo del niño).
// - Abajo, el historial de los últimos 7 días: cuándo se activó, cuándo se desactivó y desde dónde.
// La API solo devuelve las alertas que le tocan a cada quien.
// ============================================================

import { api } from "../api.js";
import { el, hora, haceCuanto, fechaHora, avisoBreve, confirmar } from "../ui.js";

const INTERVALO_MS = 10000;
const TIPOS = { Sos: "SOS" };

export function vistaAlertas(contenedor) {
    const indicador = el("span", { class: "en-vivo" }, "Conectando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const activas = el("div", { class: "alertas-activas" });
    const cuerpo = el("tbody", {}, filaMensaje("Cargando…"));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {},
                el("h1", {}, "Alertas"),
                el("p", { class: "sub" }, "Los SOS se quedan activos hasta que alguien los desactiva, desde la pulsera o aquí.")),
            indicador),
        aviso,
        activas,
        el("section", { "aria-label": "Historial de alertas" },
            el("h2", { class: "subtitulo-seccion" }, "Últimos 7 días"),
            el("div", { class: "tabla-marco" },
                el("table", { class: "tabla-tarjetas" },
                    el("thead", {}, el("tr", {},
                        el("th", { scope: "col" }, "Alerta"),
                        el("th", { scope: "col" }, "Alumno"),
                        el("th", { scope: "col" }, "Grupo"),
                        el("th", { scope: "col" }, "Dónde"),
                        el("th", { scope: "col" }, "Se activó"),
                        el("th", { scope: "col" }, "Se desactivó"))),
                    cuerpo))));

    let detenido = false;

    async function cargar() {
        try {
            const lista = await api("/api/alertas");
            if (detenido) return;
            aviso.hidden = true;
            indicador.classList.remove("desconectado");
            indicador.textContent = `Actualizado ${new Date().toLocaleTimeString("es-MX", { hour: "2-digit", minute: "2-digit" })}`;
            dibujar(lista);
        } catch (e) {
            if (detenido) return;
            aviso.textContent = `${e.message} Se reintentará en unos segundos.`;
            aviso.hidden = false;
            indicador.classList.add("desconectado");
            indicador.textContent = "Sin conexión";
        }
    }

    function dibujar(lista) {
        const abiertas = lista.filter((a) => !a.fin);
        activas.replaceChildren(...(abiertas.length === 0
            ? [el("div", { class: "aviso aviso-info" }, "No hay alertas activas.")]
            : abiertas.map(tarjetaActiva)));
        const cerradas = lista.filter((a) => a.fin);
        cuerpo.replaceChildren(...(cerradas.length === 0
            ? [filaMensaje("Sin alertas en los últimos 7 días.")]
            : cerradas.map(filaHistorial)));
    }

    function tarjetaActiva(a) {
        return el("article", { class: "alerta-activa", role: "alert", "aria-label": `${TIPOS[a.tipo] ?? a.tipo} de ${a.alumno}` },
            el("div", {},
                el("p", { class: "alerta-activa-titulo" }, `${TIPOS[a.tipo] ?? a.tipo} · ${a.alumno}`),
                el("p", {},
                    [a.grupo && `Grupo ${a.grupo}`, a.zona && `en ${a.zona}`, `desde las ${hora(a.inicio)} (${haceCuanto(a.inicio)})`]
                        .filter(Boolean).join(" · ")),
                !a.puedoDesactivar && el("p", { class: "alerta-activa-nota" }, "Solo el director o el maestro de su grupo pueden desactivarla.")),
            a.puedoDesactivar && el("button", { type: "button", class: "boton boton-blanco", onclick: () => desactivar(a) }, "Desactivar"));
    }

    async function desactivar(a) {
        const ok = await confirmar({
            titulo: `¿Desactivar el SOS de ${a.alumno}?`,
            mensaje: "Hazlo cuando alguien de la escuela ya haya visto al niño y esté bien. Quedará registrado que lo desactivaste tú.",
            aceptar: "Desactivar SOS",
        });
        if (!ok) return;
        try {
            await api(`/api/alertas/${a.id}/desactivar`, { metodo: "POST" });
            avisoBreve("SOS desactivado");
            window.dispatchEvent(new Event("alertas-cambiaron"));   // el contador del menú se actualiza ya
            cargar();
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
        }
    }

    cargar();
    const temporizador = setInterval(cargar, INTERVALO_MS);
    return () => { detenido = true; clearInterval(temporizador); };
}

function filaHistorial(a) {
    const desde = a.desactivadaDesde === "Pulsera" ? "desde la pulsera"
        : a.desactivadaPor ? `por ${a.desactivadaPor}` : "desde la página";
    return el("tr", {},
        el("td", { class: "principal" }, TIPOS[a.tipo] ?? a.tipo),
        el("td", { "data-etiqueta": "Alumno" }, a.alumno),
        el("td", { "data-etiqueta": "Grupo" }, a.grupo ?? el("span", { class: "secundario" }, "—")),
        el("td", { "data-etiqueta": "Dónde" }, a.zona ?? "—"),
        el("td", { "data-etiqueta": "Se activó", class: "sin-corte" }, fechaHora(a.inicio)),
        el("td", { "data-etiqueta": "Se desactivó" }, `${hora(a.fin)} ${desde} (${duracion(a.inicio, a.fin)})`));
}

// "a los 3 min", "a los 45 s"
function duracion(inicio, fin) {
    const s = Math.max(0, Math.round((new Date(fin) - new Date(inicio)) / 1000));
    if (s < 60) return `a los ${s} s`;
    const m = Math.round(s / 60);
    return m < 60 ? `a los ${m} min` : `a las ${Math.floor(m / 60)} h ${m % 60} min`;
}

function filaMensaje(texto) {
    return el("tr", {}, el("td", { class: "vacio", colspan: 6 }, texto));
}
