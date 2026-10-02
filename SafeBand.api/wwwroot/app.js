// ============================================================
// SafeBand · Frontend de la prueba de concepto
// Pide las lecturas a la API y las muestra. Se actualiza cada 5 s.
// ============================================================

// La página se sirve desde la misma API, así que basta la ruta relativa.
const API_LECTURAS = "/api/lecturas?limit=50";
const INTERVALO_MS = 5000;

const elUltima = document.getElementById("ultima");
const elHistorial = document.getElementById("historial");
const elError = document.getElementById("error");
const elActualizado = document.getElementById("actualizado");

// ---------- Carga de datos ----------

async function cargar() {
    try {
        const resp = await fetch(API_LECTURAS);
        if (!resp.ok) throw new Error(`La API respondió ${resp.status}`);

        const lecturas = await resp.json();   // arreglo, la más reciente primero
        ocultarError();

        if (lecturas.length === 0) {
            mostrarSinDatos();
        } else {
            mostrarUltima(lecturas[0]);
            mostrarHistorial(lecturas);
        }
        elActualizado.textContent = "Actualizado " + new Date().toLocaleTimeString("es-MX");
    } catch (err) {
        // Si falla, se conserva lo último que se mostró y se avisa arriba
        mostrarError("No se pudo conectar con la API. Se reintentará en unos segundos. (" + err.message + ")");
    }
}

// ---------- Mostrar en pantalla ----------

function mostrarUltima(l) {
    elUltima.replaceChildren();

    const valor = crear("div", "rssi-grande", `${l.rssi} ${l.unidad}`);
    const nivel = crear("div", "nivel " + claseSenal(l.rssi), textoSenal(l.rssi));

    const datos = crear("dl", "datos");
    agregarDato(datos, "Fecha y hora", formatearFecha(l.timestamp));
    agregarDato(datos, "Pulsera", l.pulsera + (l.alumno ? ` (${l.alumno})` : ""));
    agregarDato(datos, "Nodo / zona", `${l.nodo} · ${l.zona}`);

    elUltima.append(valor, nivel, datos, crearEstado(l));
}

function mostrarHistorial(lecturas) {
    elHistorial.replaceChildren();
    for (const l of lecturas) {
        const fila = document.createElement("tr");
        fila.append(
            celda(l.id),
            celda(formatearFecha(l.timestamp)),
            celda(l.nodo),
            celda(l.zona),
            celda(l.pulsera),
            celda(`${l.rssi} ${l.unidad}`),
        );
        const tdEstado = document.createElement("td");
        tdEstado.append(crearEstado(l));
        fila.append(tdEstado);
        elHistorial.append(fila);
    }
}

function mostrarSinDatos() {
    elUltima.replaceChildren(crear("p", "estado", "Sin datos todavía. Esperando la primera lectura del nodo…"));
    elHistorial.replaceChildren();
    const fila = document.createElement("tr");
    const td = crear("td", "estado", "Sin lecturas registradas.");
    td.colSpan = 7;
    fila.append(td);
    elHistorial.append(fila);
}

function mostrarError(mensaje) {
    elError.textContent = mensaje;
    elError.hidden = false;
}

function ocultarError() {
    elError.hidden = true;
}

// ---------- Ayudantes ----------

// Etiquetas de estado: puesta / quitada, SOS, batería baja
function crearEstado(l) {
    const cont = crear("div", "etiquetas");
    cont.append(l.puesta ? crear("span", "etiqueta ok", "Puesta") : crear("span", "etiqueta alerta", "Quitada"));
    if (l.sos) cont.append(crear("span", "etiqueta peligro", "SOS"));
    if (l.bateriaBaja) cont.append(crear("span", "etiqueta alerta", "Batería baja"));
    return cont;
}

// La API manda la hora en UTC ("...Z"); el navegador la convierte a la hora local
function formatearFecha(iso) {
    return new Date(iso).toLocaleString("es-MX", { dateStyle: "short", timeStyle: "medium" });
}

// Referencia aproximada: más cerca de 0 dBm = más cerca del nodo
function textoSenal(rssi) {
    if (rssi >= -60) return "Señal fuerte (muy cerca del nodo)";
    if (rssi >= -75) return "Señal media (dentro de la zona)";
    return "Señal débil (lejos o fuera de la zona)";
}

function claseSenal(rssi) {
    if (rssi >= -60) return "fuerte";
    if (rssi >= -75) return "media";
    return "debil";
}

// textContent (no innerHTML) para que ningún dato se interprete como código
function crear(tag, clase, texto) {
    const el = document.createElement(tag);
    if (clase) el.className = clase;
    if (texto !== undefined) el.textContent = texto;
    return el;
}

function celda(texto) {
    return crear("td", null, String(texto));
}

function agregarDato(dl, etiqueta, valor) {
    dl.append(crear("dt", null, etiqueta), crear("dd", null, valor));
}

// ---------- Arranque ----------
cargar();
setInterval(cargar, INTERVALO_MS);
