// ============================================================
// Pantalla de inicio de sesión → POST /api/auth/login
// ============================================================

import { api } from "../api.js";
import { el, logo } from "../ui.js";

export function vistaLogin(raiz, { alIniciarSesion, mensaje }) {
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: !mensaje }, mensaje ?? "");

    const email = el("input", { id: "email", type: "email", autocomplete: "username", required: true });
    const password = el("input", { id: "password", type: "password", autocomplete: "current-password", required: true });
    const recordarme = el("input", { type: "checkbox", checked: true });
    const boton = el("button", { type: "submit", class: "boton boton-primario" }, "Iniciar sesión");

    async function enviar(evento) {
        evento.preventDefault();
        aviso.hidden = true;
        boton.disabled = true;
        boton.textContent = "Iniciando sesión…";
        try {
            const usuario = await api("/api/auth/login", {
                metodo: "POST",
                cuerpo: { email: email.value.trim(), password: password.value, recordarme: recordarme.checked },
            });
            alIniciarSesion(usuario);
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
            password.value = "";
            password.focus();
            boton.disabled = false;
            boton.textContent = "Iniciar sesión";
        }
    }

    raiz.append(el("div", { class: "login" },
        el("section", { class: "login-marca" },
            ondas(),
            el("div", { class: "marca" }, logo(32), "SafeBand"),
            el("div", {},
                el("p", { class: "login-lema" }, "Dónde están tus alumnos, en este momento."),
                el("p", { class: "login-lema-sub" },
                    "Cada pulsera avisa en qué zona de la escuela está. Aquí lo ves en vivo, con alertas de SOS y de pulsera retirada."))),
        el("section", { class: "login-panel" },
            el("form", { class: "login-form", onsubmit: enviar, novalidate: false },
                el("div", {},
                    el("h1", {}, "Inicia sesión"),
                    el("p", { class: "ayuda" }, "Usa el correo que registró la escuela.")),
                aviso,
                el("div", { class: "campo" }, el("label", { for: "email" }, "Correo"), email),
                el("div", { class: "campo" }, el("label", { for: "password" }, "Contraseña"), password),
                el("label", { class: "casilla" }, recordarme, "Mantener la sesión iniciada en este equipo"),
                boton))));

    email.focus();
}

// Ondas concéntricas: la pulsera anunciándose por Bluetooth
function ondas() {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("class", "ondas");
    svg.setAttribute("viewBox", "0 0 560 560");
    svg.setAttribute("aria-hidden", "true");
    svg.innerHTML =
        '<g>' +
        '<circle class="onda" cx="280" cy="280" r="270" stroke-width="2"/>' +
        '<circle class="onda" cx="280" cy="280" r="270" stroke-width="2"/>' +
        '<circle class="onda" cx="280" cy="280" r="270" stroke-width="2"/>' +
        '</g>' +
        '<circle class="fija" cx="280" cy="280" r="90" stroke-width="2"/>' +
        '<circle class="fija" cx="280" cy="280" r="170" stroke-width="2"/>' +
        '<circle class="fija" cx="280" cy="280" r="250" stroke-width="2"/>' +
        '<circle cx="280" cy="280" r="34" style="fill:#1D5FC2;stroke:none"/>';
    return svg;
}
