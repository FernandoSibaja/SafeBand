// ============================================================
// Dibujo del plano de la escuela (SVG)
// Las zonas son rectángulos en METROS: { id, nombre, x, y, ancho, alto }.
// (0, 0) = esquina superior izquierda; x crece a la derecha, y hacia abajo.
// El SVG usa los metros directamente (viewBox) y se escala solo al ancho de la pantalla.
// Lo usan la pantalla "Zonas y nodos" (vista previa) y "Mis hijos".
// ============================================================

const NS = "http://www.w3.org/2000/svg";
const MARGEN = 0.6;   // metros de aire alrededor del plano

// Crea un elemento SVG con atributos e hijos
function svg(etiqueta, atributos = {}, ...hijos) {
    const nodo = document.createElementNS(NS, etiqueta);
    for (const [clave, valor] of Object.entries(atributos)) {
        if (valor == null || valor === false) continue;
        nodo.setAttribute(clave, valor);
    }
    for (const h of hijos.flat()) if (h != null && h !== false) nodo.append(h);
    return nodo;
}

/** Tamaño total del plano: hasta dónde llega la zona más lejana. */
export function medidasPlano(zonas) {
    return {
        ancho: Math.max(0, ...zonas.map((z) => z.x + z.ancho)),
        alto: Math.max(0, ...zonas.map((z) => z.y + z.alto)),
    };
}

/** ¿Se enciman dos rectángulos? (tocarse en el borde no cuenta) */
export function seEnciman(a, b) {
    return a.x < b.x + b.ancho && b.x < a.x + a.ancho && a.y < b.y + b.alto && b.y < a.y + a.alto;
}

/**
 * Devuelve un <svg> con el plano.
 * - resaltada: id de la zona que se pinta en azul (o null).
 * - estilo: cómo se pinta la resaltada: "actual" (azul), "antigua" (gris punteado) o "sos" (rojo).
 * - marcador: texto corto (p. ej. la inicial del niño) que se dibuja en un círculo dentro de la resaltada.
 * - alClic(zona): si se pasa, cada zona se puede tocar o elegir con teclado.
 * - cuadricula: dibuja líneas cada metro (útil al capturar medidas).
 * - etiqueta: descripción para lectores de pantalla.
 */
export function dibujarPlano(zonas, {
    resaltada = null, estilo = "actual", marcador = null, alClic = null, cuadricula = false, etiqueta = "Plano de la escuela",
} = {}) {
    const { ancho, alto } = medidasPlano(zonas);
    const vista = `${-MARGEN} ${-MARGEN} ${ancho + 2 * MARGEN} ${alto + 2 * MARGEN}`;
    const letraBase = Math.min(1.4, Math.max(0.35, Math.max(ancho, alto) / 16));

    const raiz = svg("svg", { class: "plano-svg", viewBox: vista, role: "img", "aria-label": etiqueta, preserveAspectRatio: "xMidYMid meet" });

    if (cuadricula) {
        const lineas = [];
        for (let x = 0; x <= Math.ceil(ancho); x++) lineas.push(svg("line", { x1: x, y1: 0, x2: x, y2: Math.ceil(alto) }));
        for (let y = 0; y <= Math.ceil(alto); y++) lineas.push(svg("line", { x1: 0, y1: y, x2: Math.ceil(ancho), y2: y }));
        raiz.append(svg("g", { class: "plano-cuadricula" }, lineas));
    }

    // La resaltada se dibuja al final para que su borde quede encima de las vecinas
    const ordenadas = [...zonas].sort((a, b) => (a.id === resaltada) - (b.id === resaltada));
    for (const z of ordenadas) {
        const esResaltada = z.id === resaltada;
        // Letra más chica si el nombre no cabe a lo ancho de la zona
        const letra = Math.min(letraBase, (z.ancho * 0.9) / Math.max(4, z.nombre.length * 0.55), z.alto * 0.45);
        const grupo = svg("g", {
            class: ["plano-zona", esResaltada && "resaltada", esResaltada && estilo, alClic && "tocable"].filter(Boolean).join(" "),
            tabindex: alClic ? "0" : null,
            role: alClic ? "button" : null,
            "aria-label": alClic ? `Editar zona ${z.nombre}` : null,
        },
            svg("title", {}, `${z.nombre} · ${z.ancho} × ${z.alto} m`),
            svg("rect", { x: z.x, y: z.y, width: z.ancho, height: z.alto, rx: Math.min(0.3, z.ancho / 10, z.alto / 10), "vector-effect": "non-scaling-stroke" }),
            svg("text", { x: z.x + z.ancho / 2, y: z.y + z.alto / 2, "font-size": letra, "text-anchor": "middle", "dominant-baseline": "central" }, z.nombre));
        if (alClic) {
            grupo.addEventListener("click", () => alClic(z));
            grupo.addEventListener("keydown", (e) => {
                if (e.key === "Enter" || e.key === " ") { e.preventDefault(); alClic(z); }
            });
        }
        raiz.append(grupo);

        // Marcador: círculo con texto en la esquina superior derecha de la zona resaltada
        if (esResaltada && marcador) {
            const r = Math.min(letraBase * 0.95, z.ancho / 4, z.alto / 4);
            const cx = z.x + z.ancho - r - Math.min(0.3, r / 2);
            const cy = z.y + r + Math.min(0.3, r / 2);
            raiz.append(svg("g", { class: `plano-marcador ${estilo}`, "aria-hidden": "true" },
                svg("circle", { cx, cy, r, "vector-effect": "non-scaling-stroke" }),
                svg("text", { x: cx, y: cy, "font-size": r * 1.1, "text-anchor": "middle", "dominant-baseline": "central" }, marcador)));
        }
    }
    return raiz;
}
