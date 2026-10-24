// ============================================================
// SafeBand · arranque del panel
// 1. Pregunta a la API quién es el usuario (/api/auth/yo).
// 2. Sin sesión → pantalla de inicio de sesión.
// 3. Con sesión → panel con menú; cada sección es una "vista".
// La navegación usa la parte "#/..." de la dirección (ej. #/vivo, #/alumnos).
// ============================================================

import { api, ErrorApi } from "./api.js";
import { el, icono, logo } from "./ui.js";
import { vistaLogin } from "./vistas/login.js";
import { vistaVivo } from "./vistas/vivo.js";
import { vistaAlumnos } from "./vistas/alumnos.js";
import { vistaGrupos } from "./vistas/grupos.js";
import { vistaPersonal } from "./vistas/personal.js";
import { vistaTutores } from "./vistas/tutores.js";
import { vistaZonas } from "./vistas/zonas.js";
import { vistaPulseras } from "./vistas/pulseras.js";
import { vistaHijos } from "./vistas/hijos.js";
import { vistaMiGrupo } from "./vistas/migrupo.js";
import { vistaEscuela } from "./vistas/escuela.js";
import { vistaAlertas } from "./vistas/alertas.js";
import { vistaDatosEscuela } from "./vistas/datos-escuela.js";

const raiz = document.getElementById("app");

// Secciones del menú: qué roles pueden verlas y qué vista dibujan
const SECCIONES = [
    { ruta: "hijos",    titulo: "Mis hijos",       icono: "alumnos",  roles: ["Padre"], vista: vistaHijos },
    { ruta: "grupo",    titulo: "Mi grupo",        icono: "grupos",   roles: ["Maestro"], vista: vistaMiGrupo },
    { ruta: "escuela",  titulo: "Panel de la escuela", icono: "zonas", roles: ["Administrador"], vista: vistaEscuela },
    { ruta: "alertas",  titulo: "Alertas",         icono: "alerta",   roles: ["Administrador", "Maestro"], vista: vistaAlertas, contador: true },
    { ruta: "vivo",     titulo: "En vivo",         icono: "vivo",     roles: ["Administrador"], vista: vistaVivo },
    { ruta: "alumnos",  titulo: "Alumnos",         icono: "alumnos",  roles: ["Administrador"], vista: vistaAlumnos },
    { ruta: "grupos",   titulo: "Grupos",          icono: "grupos",   roles: ["Administrador"], vista: vistaGrupos },
    { ruta: "pulseras", titulo: "Pulseras",        icono: "pulseras", roles: ["Administrador"], vista: vistaPulseras },
    { ruta: "tutores",  titulo: "Tutores",         icono: "tutores",  roles: ["Administrador"], vista: vistaTutores },
    { ruta: "zonas",    titulo: "Zonas y nodos",   icono: "zonas",    roles: ["Administrador"], vista: vistaZonas },
    { ruta: "personal", titulo: "Personal",        icono: "personal", roles: ["Administrador"], vista: vistaPersonal },
    { ruta: "datos-escuela", titulo: "Datos de la escuela", icono: "escuela", roles: ["Administrador"], vista: vistaDatosEscuela },
];

let usuario = null;          // quién inició sesión (o null)
let limpiarVista = null;     // función para detener la vista anterior (ej. su actualización automática)

// ---------- Sesión ----------

async function iniciar() {
    try {
        usuario = await api("/api/auth/yo");
    } catch (e) {
        if (e instanceof ErrorApi && e.estado === 401) {
            usuario = null;
        } else {
            mostrarSinConexion(e.message);
            return;
        }
    }
    dibujar();
    vigilarAlertas();
}

// ---------- Contador de alertas activas (menú y título de la pestaña) ----------
// Para el personal: cada 15 s pregunta cuántas alertas activas le tocan. Así un SOS se nota
// aunque esté en otra sección. Con alertas, la pestaña dice "(1) ..." y el contador se pone rojo.

const INTERVALO_ALERTAS_MS = 15000;
let alertasActivas = 0;
let temporizadorAlertas = null;

async function revisarAlertas() {
    if (!usuario || !usuario.roles.some((r) => r === "Administrador" || r === "Maestro")) return;
    try {
        alertasActivas = (await api("/api/alertas?activas=true")).length;
    } catch { return; }   // sin conexión: se deja el último valor
    pintarContador();
}

function pintarContador() {
    for (const c of document.querySelectorAll(".menu-contador")) {
        c.textContent = String(alertasActivas);
        c.hidden = alertasActivas === 0;
    }
    document.title = document.title.replace(/^\(\d+\) /, "");
    if (alertasActivas > 0) document.title = `(${alertasActivas}) ${document.title}`;
}

function vigilarAlertas() {
    clearInterval(temporizadorAlertas);
    temporizadorAlertas = null;
    alertasActivas = 0;
    if (!usuario) return;
    revisarAlertas();
    temporizadorAlertas = setInterval(revisarAlertas, INTERVALO_ALERTAS_MS);
}
window.addEventListener("alertas-cambiaron", revisarAlertas);

function alIniciarSesion(datos) {
    usuario = datos;
    if (!location.hash) location.hash = "#/";   // sin ruta → la primera sección de su rol
    dibujar();
    vigilarAlertas();
}

async function cerrarSesion() {
    try { await api("/api/auth/logout", { metodo: "POST" }); } catch { /* aunque falle, se sale */ }
    usuario = null;
    vigilarAlertas();   // deja de preguntar
    dibujar();
}

// Si la API dice 401 a mitad del uso (sesión vencida), volver al login
window.addEventListener("sesion-vencida", () => {
    if (!usuario) return;
    usuario = null;
    vigilarAlertas();
    dibujar("Tu sesión terminó. Inicia sesión de nuevo.");
});

window.addEventListener("hashchange", () => { if (usuario) dibujar(); });

// ---------- Dibujo ----------

function dibujar(mensajeLogin) {
    limpiarVista?.();
    limpiarVista = null;
    raiz.replaceChildren();

    if (!usuario) {
        limpiarVista = vistaLogin(raiz, { alIniciarSesion, mensaje: mensajeLogin });
        return;
    }

    const permitidas = SECCIONES.filter((s) => s.roles.some((r) => usuario.roles.includes(r)));
    const rutaPedida = location.hash.replace(/^#\//, "");
    const seccion = permitidas.find((s) => s.ruta === rutaPedida) ?? permitidas[0];
    if (!seccion) {
        mostrarSinPermiso();
        return;
    }

    const contenido = el("main", { class: "contenido", id: "contenido" });
    const panel = el("div", { class: "panel" },
        barraMovil(),
        lateral(permitidas, seccion),
        contenido);
    raiz.append(panel);
    document.title = `${seccion.titulo} · SafeBand`;
    pintarContador();

    // En celular: el menú se cierra al tocar fuera de él o con Esc
    panel.addEventListener("click", (e) => {
        if (panel.classList.contains("menu-abierto") && !e.target.closest(".lateral, .barra-movil"))
            panel.classList.remove("menu-abierto");
    });
    panel.addEventListener("keydown", (e) => {
        if (e.key === "Escape") panel.classList.remove("menu-abierto");
    });

    limpiarVista = seccion.vista(contenido, { usuario });
}

function lateral(permitidas, actual) {
    return el("aside", { class: "lateral", id: "lateral" },
        el("a", { class: "marca", href: "#/" }, logo(), "SafeBand"),
        el("nav", { class: "menu", "aria-label": "Secciones" },
            permitidas.map((s) =>
                el("a", {
                    href: `#/${s.ruta}`,
                    "aria-current": s === actual ? "page" : null,
                    onclick: () => document.querySelector(".panel")?.classList.remove("menu-abierto"),
                }, icono(s.icono), s.titulo,
                    s.contador && el("span", { class: "menu-contador", "aria-label": "alertas activas", hidden: true })))),
        el("div", { class: "usuario" },
            el("div", {},
                el("p", { class: "usuario-nombre" }, usuario.nombre || usuario.email),
                el("p", { class: "usuario-rol" }, usuario.roles.join(", "))),
            el("button", { type: "button", class: "boton boton-sutil", onclick: cerrarSesion },
                icono("salir", 18), "Cerrar sesión")));
}

function barraMovil() {
    const boton = el("button", {
        type: "button",
        class: "boton boton-sutil",
        "aria-controls": "lateral",
        "aria-label": "Abrir menú",
        onclick: () => document.querySelector(".panel").classList.toggle("menu-abierto"),
    }, icono("menu"), el("span", { class: "menu-contador", "aria-label": "alertas activas", hidden: true }));
    return el("header", { class: "barra-movil" }, el("a", { class: "marca", href: "#/" }, logo(24), "SafeBand"), boton);
}

function mostrarSinConexion(mensaje) {
    raiz.replaceChildren(el("div", { class: "contenido" },
        el("div", { class: "aviso aviso-error" }, mensaje),
        el("p", { style: "margin-top:16px" },
            el("button", { type: "button", class: "boton boton-primario", onclick: iniciar }, "Reintentar"))));
}

function mostrarSinPermiso() {
    raiz.replaceChildren(el("div", { class: "contenido" },
        el("div", { class: "aviso aviso-info" }, "Tu cuenta todavía no tiene secciones asignadas. Pide acceso al administrador de la escuela."),
        el("p", { style: "margin-top:16px" },
            el("button", { type: "button", class: "boton boton-sutil", onclick: cerrarSesion }, "Cerrar sesión"))));
}

iniciar();
