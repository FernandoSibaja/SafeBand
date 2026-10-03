// ============================================================
// Vista "En vivo" → GET /api/lecturas?limit=50, cada 5 segundos
// Muestra dónde se detectó la pulsera por última vez y el historial.
// Estados: cargando, sin datos, error de conexión (conserva lo último).
// ============================================================

import { api } from "../api.js";
import { el, hora, cuando, esReciente, fechaHora, haceCuanto, barrasSenal, textoSenal, etiquetasEstado } from "../ui.js";

const INTERVALO_MS = 5000;

export function vistaVivo(contenedor) {
    const indicador = el("span", { class: "en-vivo" }, "Conectando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const ahora = el("section", { class: "ahora", "aria-label": "Última detección" },
        el("p", { class: "vacio" }, "Cargando la última detección…"));
    const cuerpoTabla = el("tbody", {},
        el("tr", {}, el("td", { class: "vacio", colspan: 6 }, "Cargando lecturas…")));

    contenedor.append(el("div", { class: "vivo" },
        el("header", { class: "encabezado" },
            el("div", {},
                el("h1", {}, "En vivo"),
                el("p", { class: "sub" }, "Lo que detectan los nodos de la escuela. Se actualiza solo cada 5 segundos.")),
            indicador),
        aviso,
        ahora,
        el("section", { class: "seccion" },
            el("h2", {}, "Últimas lecturas"),
            el("div", { class: "tabla-marco" },
                el("table", {},
                    el("thead", {}, el("tr", {},
                        el("th", { scope: "col" }, "Hora"),
                        el("th", { scope: "col" }, "Alumno / pulsera"),
                        el("th", { scope: "col" }, "Zona"),
                        el("th", { scope: "col" }, "Nodo"),
                        el("th", { scope: "col" }, "Señal"),
                        el("th", { scope: "col" }, "Estado"))),
                    cuerpoTabla)))));

    let detenido = false;

    async function cargar() {
        try {
            const lecturas = await api("/api/lecturas?limit=50");
            if (detenido) return;
            aviso.hidden = true;
            indicador.classList.remove("desconectado");
            indicador.textContent = `Actualizado ${new Date().toLocaleTimeString("es-MX")}`;
            dibujarAhora(lecturas[0]);
            dibujarTabla(lecturas);
        } catch (e) {
            if (detenido) return;
            // Se conserva lo último que se mostró; solo se avisa
            aviso.textContent = `${e.message} Se reintentará en unos segundos.`;
            aviso.hidden = false;
            indicador.classList.add("desconectado");
            indicador.textContent = "Sin conexión";
        }
    }

    function dibujarAhora(l) {
        ahora.classList.toggle("sos", !!l?.sos);
        if (!l) {
            ahora.replaceChildren(el("p", { class: "vacio" },
                "Todavía no hay detecciones. Enciende una pulsera cerca de un nodo y aparecerá aquí."));
            return;
        }
        ahora.replaceChildren(
            el("div", {},
                el("p", { class: "ahora-zona" }, l.zona),
                el("p", { class: "ahora-quien" },
                    l.alumno ?? "Pulsera sin asignar",
                    el("span", { class: "pulsera" }, `  ${l.pulsera}`)),
                el("div", { class: "ahora-detalle" },
                    el("span", {}, esReciente(l.timestamp)
                        ? `Detectada ${haceCuanto(l.timestamp)} (${hora(l.timestamp)})`
                        : `Detectada el ${fechaHora(l.timestamp)}`),
                    el("span", {}, `Nodo ${l.nodo}`)),
                el("div", { style: "margin-top:12px" }, etiquetasEstado(l))),
            el("div", { class: "ahora-senal" },
                barrasSenal(l.rssi, true),
                el("span", { class: "dbm" }, `${l.rssi} dBm`),
                el("span", { class: "distancia" }, textoSenal(l.rssi))));
    }

    function dibujarTabla(lecturas) {
        if (lecturas.length === 0) {
            cuerpoTabla.replaceChildren(el("tr", {},
                el("td", { class: "vacio", colspan: 6 }, "Sin lecturas registradas.")));
            return;
        }
        cuerpoTabla.replaceChildren(...lecturas.map((l) => el("tr", {},
            el("td", { class: "num" }, cuando(l.timestamp)),
            el("td", {},
                l.alumno ?? el("span", { class: "secundario" }, "Sin asignar"),
                el("span", { class: "secundario" }, `  ${l.pulsera}`)),
            el("td", {}, l.zona),
            el("td", {}, l.nodo),
            el("td", { class: "num" }, barrasSenal(l.rssi), `  ${l.rssi} dBm`),
            el("td", {}, etiquetasEstado(l)))));
    }

    cargar();
    const temporizador = setInterval(cargar, INTERVALO_MS);

    // Al salir de esta vista se detiene la actualización automática
    return () => {
        detenido = true;
        clearInterval(temporizador);
    };
}
