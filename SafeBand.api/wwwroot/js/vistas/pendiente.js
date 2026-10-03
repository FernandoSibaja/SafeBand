// Vista temporal para las secciones que todavía no se construyen.
import { el } from "../ui.js";

export function vistaPendiente(contenedor, titulo, descripcion) {
    contenedor.append(
        el("header", { class: "encabezado" },
            el("div", {}, el("h1", {}, titulo), el("p", { class: "sub" }, descripcion))),
        el("div", { class: "pendiente" },
            el("p", {}, el("strong", {}, "Esta sección se construye en el siguiente paso. "),
                "Mientras tanto, se puede administrar desde ",
                el("a", { href: "/swagger" }, "Swagger"), ".")));
}
