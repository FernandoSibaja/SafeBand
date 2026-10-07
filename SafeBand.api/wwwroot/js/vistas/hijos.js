// ============================================================
// Vista "Mis hijos" (rol Padre) → GET /api/mis-hijos, cada 10 segundos
// Una tarjeta por hijo: dónde está ahora, su estado y su recorrido de hoy.
// La API solo devuelve los hijos de este padre.
// ============================================================

import { api } from "../api.js";
import { el, hora, haceCuanto, fechaHora, esReciente } from "../ui.js";

const INTERVALO_MS = 10000;
const MINUTOS_RECIENTE = 30;   // si la última detección es más vieja, se dice "último lugar donde se detectó"

export function vistaHijos(contenedor) {
    const indicador = el("span", { class: "en-vivo" }, "Conectando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const lista = el("div", { class: "hijos-padre" }, el("p", { class: "estado" }, "Cargando…"));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {},
                el("h1", {}, "Mis hijos"),
                el("p", { class: "sub" }, "Dónde se detectó su pulsera dentro de la escuela. Se actualiza solo.")),
            indicador),
        aviso,
        lista);

    let detenido = false;

    async function cargar() {
        // "Hoy" según la hora del celular del padre: desde la medianoche local
        const medianoche = new Date();
        medianoche.setHours(0, 0, 0, 0);
        try {
            const hijos = await api(`/api/mis-hijos?desde=${encodeURIComponent(medianoche.toISOString())}`);
            if (detenido) return;
            aviso.hidden = true;
            indicador.classList.remove("desconectado");
            indicador.textContent = `Actualizado ${new Date().toLocaleTimeString("es-MX", { hour: "2-digit", minute: "2-digit" })}`;
            dibujar(hijos);
        } catch (e) {
            if (detenido) return;
            aviso.textContent = `${e.message} Se reintentará en unos segundos.`;
            aviso.hidden = false;
            indicador.classList.add("desconectado");
            indicador.textContent = "Sin conexión";
        }
    }

    function dibujar(hijos) {
        if (hijos.length === 0) {
            lista.replaceChildren(el("div", { class: "pendiente" },
                el("strong", {}, "Todavía no tienes alumnos vinculados. "),
                "La escuela debe agregarte como tutor de tus hijos. Si crees que es un error, comunícate con la escuela."));
            return;
        }
        lista.replaceChildren(...hijos.map(tarjeta));
    }

    cargar();
    const temporizador = setInterval(cargar, INTERVALO_MS);
    return () => { detenido = true; clearInterval(temporizador); };
}

function tarjeta(h) {
    const u = h.ultima;
    const reciente = u && (Date.now() - new Date(u.timestamp).getTime()) / 60000 < MINUTOS_RECIENTE;

    return el("article", { class: u?.sos ? "hijo-tarjeta sos" : "hijo-tarjeta", "aria-label": h.nombre },
        el("header", { class: "hijo-tarjeta-encabezado" },
            el("h2", {}, h.nombre),
            h.pulsera
                ? el("span", { class: "etiqueta azul" }, h.pulsera)
                : el("span", { class: "etiqueta neutra" }, "Sin pulsera")),

        // Lo más importante primero: ¿pidió ayuda?
        u?.sos && el("div", { class: "alerta-sos", role: "alert" },
            el("strong", {}, "SOS activado. "), `Presionó el botón de ayuda ${haceCuanto(u.timestamp)}. La escuela también recibe este aviso.`),

        el("div", { class: "hijo-ahora" },
            !u
                ? el("p", { class: "secundario" }, h.pulsera
                    ? "Su pulsera todavía no ha sido detectada por ningún nodo."
                    : "Todavía no tiene pulsera asignada. La escuela se la dará.")
                : [
                    el("p", { class: "hijo-ahora-etiqueta" }, reciente ? "Está en" : "Último lugar donde se detectó"),
                    el("p", { class: "hijo-ahora-zona" }, u.zona),
                    el("p", { class: "secundario" },
                        esReciente(u.timestamp) ? `${haceCuanto(u.timestamp)} (${hora(u.timestamp)})` : `el ${fechaHora(u.timestamp)}`),
                    el("div", { class: "etiquetas", style: "margin-top:10px" },
                        u.puesta
                            ? el("span", { class: "etiqueta ok" }, "Pulsera puesta")
                            : el("span", { class: "etiqueta alerta" }, "Pulsera quitada"),
                        u.bateriaBaja && el("span", { class: "etiqueta alerta" }, "Batería baja")),
                ]),

        el("section", { class: "recorrido", "aria-label": `Recorrido de hoy de ${h.nombre}` },
            el("h3", {}, "Hoy"),
            h.recorrido.length === 0
                ? el("p", { class: "secundario" }, "Sin movimientos registrados hoy.")
                : el("ol", {}, h.recorrido.map((t) => el("li", {},
                    el("span", { class: "recorrido-hora" }, rango(t)),
                    el("span", {}, t.zona))))));
}

// "8:02 – 8:40", o solo "8:02" si fue una sola detección
function rango(t) {
    const corta = (iso) => new Date(iso).toLocaleTimeString("es-MX", { hour: "numeric", minute: "2-digit" });
    const a = corta(t.desde), b = corta(t.hasta);
    return a === b ? a : `${a} – ${b}`;
}
