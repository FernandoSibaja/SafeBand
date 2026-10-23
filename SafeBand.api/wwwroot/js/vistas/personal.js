// ============================================================
// Pantalla "Personal" (solo Administrador): cuentas de maestros y administradores.
// - Lista con rol, estado de la cuenta y grupos donde es maestro titular.
// - Formulario en un panel lateral: nombre, correo y rol.
// - La cuenta se crea sin contraseña: se le genera una invitación (código impreso) y la activa
//   en «Crear mi cuenta». Un código nuevo sirve también para poner una contraseña nueva.
// - Dar de baja (ya no puede iniciar sesión) y reactivar. Nadie se da de baja a sí mismo.
// API: /api/personal
// ============================================================

import { api } from "../api.js";
import { el, icono, avisoBreve, confirmar, campo, crearCajon, normalizar, mostrarInvitacion, etiquetaCuenta } from "../ui.js";

const ROLES = [["Maestro", "Maestro"], ["Administrador", "Administrador (dirección)"]];
const textoRol = (r) => (r === "Administrador" ? "Administrador" : "Maestro");

export function vistaPersonal(contenedor) {
    let personal = [];
    let incluirInactivos = false;
    let filtro = "";
    let cerrarFormulario = null;

    const conteo = el("p", { class: "sub" }, "Cargando…");
    const aviso = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const cuerpo = el("tbody", {}, filaMensaje("Cargando personal…"));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {}, el("h1", {}, "Personal"), conteo),
            el("button", { type: "button", class: "boton boton-primario", onclick: () => abrirFormulario(null) },
                icono("mas", 18), "Agregar persona")),
        aviso,
        el("div", { class: "herramientas" },
            el("input", {
                type: "search", class: "buscador", placeholder: "Buscar por nombre o correo", "aria-label": "Buscar en el personal",
                oninput: (e) => { filtro = e.target.value; dibujarTabla(); },
            }),
            el("label", { class: "casilla" },
                el("input", { type: "checkbox", onchange: (e) => { incluirInactivos = e.target.checked; cargar(); } }),
                "Mostrar dados de baja")),
        el("div", { class: "tabla-marco" },
            el("table", { class: "tabla-tarjetas" },
                el("thead", {}, el("tr", {},
                    el("th", { scope: "col" }, "Nombre"),
                    el("th", { scope: "col" }, "Correo"),
                    el("th", { scope: "col" }, "Rol"),
                    el("th", { scope: "col" }, "Cuenta"),
                    el("th", { scope: "col" }, "Grupos"),
                    el("th", { scope: "col" }, el("span", { class: "oculto-visual" }, "Acciones")))),
                cuerpo)));

    // ---------- Lista ----------

    async function cargar() {
        try {
            personal = await api(`/api/personal${incluirInactivos ? "?incluirInactivos=true" : ""}`);
            aviso.hidden = true;
            dibujarTabla();
        } catch (e) {
            aviso.textContent = e.message;
            aviso.hidden = false;
            if (personal.length === 0) cuerpo.replaceChildren(filaMensaje("No se pudo cargar la lista."));
        }
    }

    function dibujarTabla() {
        const maestros = personal.filter((p) => p.activo && p.rol === "Maestro").length;
        const admins = personal.filter((p) => p.activo && p.rol === "Administrador").length;
        conteo.textContent = `${maestros} ${maestros === 1 ? "maestro" : "maestros"} y ${admins} ${admins === 1 ? "administrador" : "administradores"}`;

        const texto = normalizar(filtro);
        const visibles = personal.filter((p) => !texto || normalizar(`${p.nombre} ${p.email}`).includes(texto));
        cuerpo.replaceChildren(...(visibles.length === 0
            ? [filaMensaje(personal.length === 0 ? "Todavía no hay personal registrado." : `Nadie coincide con «${filtro}».`)]
            : visibles.map(fila)));
    }

    function fila(p) {
        return el("tr", { class: p.activo ? null : "inactivo" },
            el("td", { class: "principal" },
                p.nombre,
                p.eresTu && el("span", { class: "etiqueta azul", style: "margin-left:8px" }, "Tú"),
                !p.activo && el("span", { class: "etiqueta neutra", style: "margin-left:8px" }, "Dado de baja")),
            el("td", { "data-etiqueta": "Correo" }, p.email),
            el("td", { "data-etiqueta": "Rol" }, textoRol(p.rol)),
            el("td", { "data-etiqueta": "Cuenta" }, etiquetaCuenta(p.estadoCuenta)),
            el("td", { "data-etiqueta": "Grupos" }, p.grupos.length ? p.grupos.join(", ") : el("span", { class: "secundario" }, "—")),
            el("td", { class: "acciones" },
                el("button", {
                    type: "button", class: "boton boton-enlace", "aria-label": `Editar a ${p.nombre}`,
                    onclick: () => abrirFormulario(p),
                }, "Editar")));
    }

    // ---------- Formulario (panel lateral) ----------

    function abrirFormulario(persona) {
        cerrarFormulario?.();
        const editando = persona != null;

        const nombre = campo("p-nombre", "Nombre completo",
            el("input", { id: "p-nombre", autocomplete: "off", placeholder: "Laura Méndez Ruiz", value: persona?.nombre ?? "" }), { ancho: true });
        const email = campo("p-email", "Correo",
            el("input", { id: "p-email", type: "email", autocomplete: "off", value: persona?.email ?? "" }),
            { ancho: true, ayuda: "Con este correo iniciará sesión." });
        const selectorRol = el("select", { id: "p-rol", disabled: persona?.eresTu === true },
            ROLES.map(([v, t]) => el("option", { value: v }, t)));
        selectorRol.value = persona?.rol ?? "Maestro";
        const rol = campo("p-rol", "Rol", selectorRol, {
            ancho: true,
            ayuda: persona?.eresTu
                ? "No puedes cambiar tu propio rol."
                : "Maestro: ve a sus grupos. Administrador: administra toda la escuela.",
        });

        const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
        const botonGuardar = el("button", { type: "submit", class: "boton boton-primario" }, editando ? "Guardar cambios" : "Agregar persona");
        const accionEstado = !editando || persona.eresTu ? null
            : persona.activo
                ? el("button", { type: "button", class: "boton boton-peligro", onclick: darDeBaja }, "Dar de baja")
                : el("button", { type: "button", class: "boton boton-sutil", onclick: reactivar }, "Reactivar");

        const { cerrar } = crearCajon({
            titulo: editando ? persona.nombre : "Agregar persona",
            subtitulo: editando ? "Sus datos, su rol y su acceso a la app." : "Un maestro o alguien de la dirección. Después le generas su invitación.",
            contenido: [
                avisoForm,
                el("div", { class: "grupo-campos" }, nombre.nodo, email.nodo, rol.nodo),
                editando && seccionCuenta(),
            ],
            pie: [
                accionEstado,
                el("div", { class: "derecha" },
                    el("button", { type: "button", class: "boton boton-sutil", onclick: () => cerrar() }, "Cancelar"),
                    botonGuardar),
            ],
            alEnviar: guardar,
            alCerrar: () => { cerrarFormulario = null; },
        });
        cerrarFormulario = cerrar;
        nombre.input.focus();

        function seccionCuenta() {
            const textos = {
                CuentaActiva: "Ya activó su cuenta. Si olvidó su contraseña, genérale un código para que ponga una nueva.",
                InvitacionPendiente: "Tiene un código sin usar. Genera uno nuevo si se perdió o venció.",
                SinInvitar: "Todavía no puede entrar: genérale su invitación.",
            };
            const textoBoton = {
                CuentaActiva: "Generar código para nueva contraseña",
                InvitacionPendiente: "Generar un código nuevo",
                SinInvitar: "Generar invitación",
            };
            return el("div", { class: "separador-form" },
                el("h3", {}, "Acceso a la app"),
                el("p", { style: "margin-bottom:10px" }, etiquetaCuenta(persona.estadoCuenta), " ", textos[persona.estadoCuenta]),
                persona.activo && el("button", { type: "button", class: "boton boton-sutil", onclick: generarInvitacion },
                    textoBoton[persona.estadoCuenta]));
        }

        async function generarInvitacion() {
            if (persona.estadoCuenta !== "SinInvitar") {
                const ok = await confirmar({
                    titulo: persona.estadoCuenta === "CuentaActiva" ? "¿Generar código para nueva contraseña?" : "¿Generar un código nuevo?",
                    mensaje: persona.estadoCuenta === "CuentaActiva"
                        ? "Con el código podrá poner una contraseña nueva en «Crear mi cuenta». Su contraseña actual sigue funcionando hasta que lo use."
                        : "El código anterior dejará de funcionar.",
                    aceptar: "Generar código",
                });
                if (!ok) return;
            }
            try {
                const invitacion = await api(`/api/personal/${persona.id}/invitacion`, { metodo: "POST" });
                cerrar();
                cargar();
                mostrarInvitacion(invitacion, {
                    titulo: persona.estadoCuenta === "CuentaActiva" ? "Código para poner una contraseña nueva" : "Invitación para crear tu cuenta",
                    nota: persona.rol === "Maestro"
                        ? "Con tu cuenta verás dónde están los alumnos de tus grupos y recibirás sus avisos. "
                        : "Con tu cuenta administrarás SafeBand en la escuela. ",
                });
            } catch (e) {
                mostrarAviso(e.message);
            }
        }

        async function guardar(evento) {
            evento.preventDefault();
            avisoForm.hidden = true;
            for (const c of [nombre, email, rol]) c.limpiar();
            botonGuardar.disabled = true;
            const texto = botonGuardar.textContent;
            botonGuardar.textContent = "Guardando…";
            try {
                const datos = { nombre: nombre.input.value.trim(), email: email.input.value.trim(), rol: selectorRol.value };
                if (editando) await api(`/api/personal/${persona.id}`, { metodo: "PUT", cuerpo: datos });
                else await api("/api/personal", { metodo: "POST", cuerpo: datos });
                cerrar();
                avisoBreve(editando ? "Cambios guardados" : "Persona agregada. Ahora genérale su invitación.");
                cargar();
            } catch (e) {
                const errores = e.datos?.errors;
                if (errores) {
                    const campos = { nombre, email, rol };
                    const sinCampo = [];
                    for (const [clave, mensajes] of Object.entries(errores)) {
                        const c = campos[clave.toLowerCase()];
                        if (c) c.ponerError(mensajes.join(" ")); else sinCampo.push(...mensajes);
                    }
                    if (sinCampo.length) mostrarAviso(sinCampo.join(" "));
                } else if (/correo/i.test(e.message)) email.ponerError(e.message);
                else if (/rol/i.test(e.message)) rol.ponerError(e.message);
                else mostrarAviso(e.message);
            } finally {
                botonGuardar.disabled = false;
                botonGuardar.textContent = texto;
            }
        }

        async function darDeBaja() {
            const ok = await confirmar({
                titulo: `¿Dar de baja a ${persona.nombre}?`,
                mensaje: "Ya no podrá iniciar sesión y se cerrarán sus sesiones abiertas. No se borra: puedes reactivarla.",
                aceptar: "Dar de baja",
                peligro: true,
            });
            if (!ok) return;
            await accion(() => api(`/api/personal/${persona.id}`, { metodo: "DELETE" }), "Cuenta dada de baja");
        }

        async function reactivar() {
            await accion(() => api(`/api/personal/${persona.id}/reactivar`, { metodo: "POST" }), "Cuenta reactivada");
        }

        async function accion(llamada, mensaje) {
            try {
                await llamada();
                cerrar();
                avisoBreve(mensaje);
                cargar();
            } catch (e) {
                mostrarAviso(e.message);
            }
        }

        function mostrarAviso(texto) {
            avisoForm.textContent = texto;
            avisoForm.hidden = false;
        }
    }

    cargar();
    return () => cerrarFormulario?.();
}

function filaMensaje(texto) {
    return el("tr", {}, el("td", { class: "vacio", colspan: 6 }, texto));
}
