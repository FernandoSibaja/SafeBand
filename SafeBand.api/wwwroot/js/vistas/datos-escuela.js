// ============================================================
// Pantalla "Datos de la escuela" (solo Administrador) → GET/PUT /api/datos-escuela
// Nombre y teléfono. El teléfono aparece en "Mis hijos" (botón "Llamar a la escuela")
// y en los avisos de SOS que reciben los padres.
// ============================================================

import { api } from "../api.js";
import { el, avisoBreve, campo } from "../ui.js";

export function vistaDatosEscuela(contenedor) {
    const nombre = campo("e-nombre", "Nombre de la escuela",
        el("input", { id: "e-nombre", autocomplete: "organization", placeholder: "Escuela Primaria Benito Juárez" }), { ancho: true });
    const telefono = campo("e-telefono", "Teléfono",
        el("input", { id: "e-telefono", type: "tel", autocomplete: "tel", placeholder: "664 123 4567" }),
        { ancho: true, opcional: true, ayuda: "Los padres lo verán en «Mis hijos» y en el aviso de SOS para llamar a la escuela." });
    const avisoForm = el("div", { class: "aviso aviso-error", role: "alert", hidden: true });
    const boton = el("button", { type: "submit", class: "boton boton-primario" }, "Guardar");

    const formulario = el("form", { class: "tarjeta-form", onsubmit: guardar },
        avisoForm,
        el("div", { class: "grupo-campos" }, nombre.nodo, telefono.nodo),
        el("div", { class: "derecha" }, boton));

    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {},
                el("h1", {}, "Datos de la escuela"),
                el("p", { class: "sub" }, "Se usan en la app y en los avisos a los padres."))),
        formulario);

    api("/api/datos-escuela").then((d) => {
        nombre.input.value = d.nombre ?? "";
        telefono.input.value = d.telefono ?? "";
    }).catch((e) => mostrar(e.message));

    async function guardar(evento) {
        evento.preventDefault();
        avisoForm.hidden = true;
        nombre.limpiar();
        telefono.limpiar();
        boton.disabled = true;
        try {
            await api("/api/datos-escuela", { metodo: "PUT", cuerpo: { nombre: nombre.input.value.trim(), telefono: telefono.input.value.trim() || null } });
            avisoBreve("Datos guardados");
        } catch (e) {
            const errores = e.datos?.errors;
            if (errores) {
                for (const [clave, mensajes] of Object.entries(errores)) {
                    const c = { nombre, telefono }[clave.toLowerCase()];
                    if (c) c.ponerError(mensajes.join(" ")); else mostrar(mensajes.join(" "));
                }
            } else mostrar(e.message);
        } finally {
            boton.disabled = false;
        }
    }

    function mostrar(texto) {
        avisoForm.textContent = texto;
        avisoForm.hidden = false;
    }
}
