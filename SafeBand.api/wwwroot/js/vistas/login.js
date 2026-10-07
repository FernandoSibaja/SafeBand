// ============================================================
// Pantalla de entrada, con dos modos:
//   - Iniciar sesión      → POST /api/auth/login
//   - Crear mi cuenta     → POST /api/auth/registro (padres, con el código que les dio la escuela)
// ============================================================

import { api } from "../api.js";
import { el, logo } from "../ui.js";

export function vistaLogin(raiz, { alIniciarSesion, mensaje }) {
    const panel = el("section", { class: "login-panel" });

    raiz.append(el("div", { class: "login" },
        el("section", { class: "login-marca" },
            ondas(),
            el("div", { class: "marca" }, logo(32), "SafeBand"),
            el("div", {},
                el("p", { class: "login-lema" }, "Dónde están los niños, en este momento."),
                el("p", { class: "login-lema-sub" },
                    "Cada pulsera avisa en qué zona de la escuela está. La escuela lo ve en vivo y cada familia ve a sus hijos, con avisos de SOS y de pulsera retirada."))),
        panel));

    function mostrarLogin(texto) {
        panel.replaceChildren(formularioLogin({ alIniciarSesion, mensaje: texto, alCrearCuenta: mostrarRegistro }));
        panel.querySelector("#email").focus();
    }
    function mostrarRegistro() {
        panel.replaceChildren(formularioRegistro({ alIniciarSesion, alVolver: () => mostrarLogin() }));
        panel.querySelector("#r-email").focus();
    }

    mostrarLogin(mensaje);
}

// ---------- Iniciar sesión ----------

function formularioLogin({ alIniciarSesion, mensaje, alCrearCuenta }) {
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

    return el("form", { class: "login-form", onsubmit: enviar },
        el("div", {},
            el("h1", {}, "Inicia sesión"),
            el("p", { class: "ayuda" }, "Usa el correo que registró la escuela.")),
        aviso,
        el("div", { class: "campo" }, el("label", { for: "email" }, "Correo"), email),
        el("div", { class: "campo" }, el("label", { for: "password" }, "Contraseña"), password),
        el("label", { class: "casilla" }, recordarme, "Mantener la sesión iniciada en este equipo"),
        boton,
        el("p", { class: "login-cambio" },
            "¿La escuela te dio un código de invitación? ",
            el("button", { type: "button", class: "boton-texto", onclick: alCrearCuenta }, "Crear mi cuenta")));
}

// ---------- Crear mi cuenta (padres) ----------

function formularioRegistro({ alIniciarSesion, alVolver }) {
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const email = el("input", { id: "r-email", type: "email", autocomplete: "email", required: true });
    const codigo = el("input", {
        id: "r-codigo", autocomplete: "one-time-code", placeholder: "K7M4-9QPX", required: true,
        autocapitalize: "characters", spellcheck: "false", style: "text-transform:uppercase;letter-spacing:.08em",
    });
    const password = el("input", { id: "r-password", type: "password", autocomplete: "new-password", required: true });
    const confirmar = el("input", { id: "r-confirmar", type: "password", autocomplete: "new-password", required: true });
    const acepta = el("input", { type: "checkbox", id: "r-acepta" });
    const boton = el("button", { type: "submit", class: "boton boton-primario" }, "Crear mi cuenta");

    function error(texto, foco) {
        aviso.textContent = texto;
        aviso.hidden = false;
        foco?.focus();
    }

    async function enviar(evento) {
        evento.preventDefault();
        aviso.hidden = true;
        if (password.value !== confirmar.value) return error("Las contraseñas no coinciden.", confirmar);
        if (!acepta.checked) return error("Para crear tu cuenta debes aceptar el aviso de privacidad.", acepta);

        boton.disabled = true;
        boton.textContent = "Creando tu cuenta…";
        try {
            const usuario = await api("/api/auth/registro", {
                metodo: "POST",
                cuerpo: {
                    email: email.value.trim(),
                    codigo: codigo.value.trim(),
                    password: password.value,
                    aceptaAvisoPrivacidad: acepta.checked,
                },
            });
            alIniciarSesion(usuario);   // la API ya dejó la sesión iniciada
        } catch (e) {
            error(e.message);
            boton.disabled = false;
            boton.textContent = "Crear mi cuenta";
        }
    }

    return el("form", { class: "login-form", onsubmit: enviar },
        el("div", {},
            el("h1", {}, "Crea tu cuenta"),
            el("p", { class: "ayuda" }, "Usa el correo que diste a la escuela y el código de tu invitación impresa.")),
        aviso,
        el("div", { class: "campo" }, el("label", { for: "r-email" }, "Correo"), email),
        el("div", { class: "campo" }, el("label", { for: "r-codigo" }, "Código de invitación"), codigo),
        el("div", { class: "campo" },
            el("label", { for: "r-password" }, "Contraseña nueva"), password,
            el("p", { class: "ayuda-campo" }, "Mínimo 8 caracteres, con mayúscula, minúscula y número.")),
        el("div", { class: "campo" }, el("label", { for: "r-confirmar" }, "Repite la contraseña"), confirmar),
        avisoPrivacidad(),
        el("label", { class: "casilla" }, acepta, "Leí y acepto el aviso de privacidad"),
        boton,
        el("p", { class: "login-cambio" },
            "¿Ya tienes cuenta? ",
            el("button", { type: "button", class: "boton-texto", onclick: alVolver }, "Inicia sesión")));
}

// BORRADOR: el texto definitivo debe revisarlo la escuela (es la responsable de los datos ante la LFPDPPP)
function avisoPrivacidad() {
    return el("details", { class: "aviso-privacidad" },
        el("summary", {}, "Leer el aviso de privacidad"),
        el("p", {}, "La escuela es responsable de los datos personales que se tratan en SafeBand."),
        el("p", {}, "Datos que se usan: tu nombre, correo y teléfono; el nombre de tus hijos y la zona de la escuela en la que su pulsera es detectada, con fecha y hora."),
        el("p", {}, "Para qué: mostrarte dónde están tus hijos dentro de la escuela, avisarte de emergencias (SOS, pulsera retirada) y verificar quién los recoge. No se usan para publicidad ni se comparten con terceros."),
        el("p", {}, "Tus derechos: puedes pedir a la escuela ver, corregir o borrar tus datos, u oponerte a su uso (derechos ARCO)."));
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
