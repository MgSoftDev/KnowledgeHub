# Diagramas: se dibujan en SVG, se entregan en PNG

KnowledgeHub **rechaza el guardado** de una página con `data:image/svg+xml`, y borra cualquier
`<svg>` inline. Pero el SVG es la mejor forma de que una IA dibuje un diagrama con precisión
(coordenadas exactas, texto seleccionable mientras se revisa, editable después). Así que la fuente
es SVG y lo que va dentro del HTML es un PNG rasterizado a partir de ella:

```
diagramas/er-common-lineas.svg      ← lo dibuja la IA y se conserva para editarlo
03-ModeloDeDatos.html               ← <img src="diagramas/er-common-lineas.svg">
python scripts/embed_images.py 03-ModeloDeDatos.html
                                    → el <img> queda con data:image/png;base64,… (2x, nítido)
```

`embed_images.py` rasteriza con Playwright (Chromium, o el Edge instalado). Si no hay Playwright en
la máquina, `svg_to_png.py` lo dice y explica cómo instalarlo. Y si de verdad no hay forma de
rasterizar, el diagrama se deja con el marcador visible de `plantillas.md` (`[IMAGEN: diagrama
diagramas/x.svg pendiente de convertir a PNG]`) y **nunca** como SVG embebido.

## Reglas de dibujo comunes

- `width`, `height` y `viewBox="0 0 W H"` iguales, `xmlns="http://www.w3.org/2000/svg"`, y un
  `<rect width="W" height="H" fill="#FFFFFF"/>` de fondo: el PNG sale con fondo blanco y del tamaño
  exacto. Ancho entre 900 y 1100 px (a 2x el PNG queda nítido; KnowledgeHub reescala a 1600 si
  hace falta).
- Fuente: `font-family="Segoe UI, Roboto, Helvetica, Arial, sans-serif"`; monoespaciada:
  `font-family="Consolas, Courier New, monospace"`.
- Título arriba a la izquierda (16px, `font-weight="700"`, `#1F4E79`) y subtítulo de una frase
  (11px, `#767676`): el diagrama se entiende solo, sin el párrafo que lo rodea.
- Sin degradados, sombras, filtros ni `<foreignObject>`: se rasterizan mal o no se rasterizan.
- Texto **dentro** del SVG con `<text>`; nunca imágenes dentro del SVG.

## Diagrama entidad-relación

Convenciones fijas, para que todos los ER de la documentación se lean igual:

| Elemento | Cómo |
|---|---|
| Tabla | `<rect rx="5" fill="#FFFFFF" stroke="#2E75B6" stroke-width="1.4">` |
| Cabecera | Franja sólida `#2E75B6` con esquinas superiores redondeadas (un `<path>`), nombre en blanco, 12.5px, `font-weight="700"`, con esquema: `Common.LineSegments` |
| Filas | Zebra manual: `<rect fill="#EEF3FA">` en las alternas |
| Columna | Nombre a la izquierda en Consolas 10.6px `#1A1A1A`; tipo SQL a la derecha con `text-anchor="end"`, 9.4px, `#767676` |
| PK / FK | `font-weight="700"`; las FK con el prefijo real (`Fk_Line`) |
| Relación | Líneas `#5B7DB1`, `stroke-width="1.3"`, ortogonales (`M x y L x y L x y`), con pata de gallo en el lado «muchos» |

Un ER completo de referencia está en `examples/diagramas/er-ejemplo.svg`; cópialo y ajusta
coordenadas. Plantilla de una tabla con tres columnas:

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="360" viewBox="0 0 1000 360">
<rect width="1000" height="360" fill="#FFFFFF"/>
<text x="20" y="30" font-family="Segoe UI, Roboto, Helvetica, Arial, sans-serif" font-size="16" font-weight="700" fill="#1F4E79">Common — Líneas y estaciones</text>
<text x="20" y="48" font-family="Segoe UI, Roboto, Helvetica, Arial, sans-serif" font-size="11" fill="#767676">La línea es la raíz del catálogo: casi todo cuelga de Common.Lines</text>

<!-- Tabla: caja, cabecera, tres filas (alto de fila 18px) -->
<rect x="40" y="80" width="200" height="83" rx="5" fill="#FFFFFF" stroke="#2E75B6" stroke-width="1.4"/>
<path d="M40 109 L40 85 Q40 80 45 80 L235 80 Q240 80 240 85 L240 109 Z" fill="#2E75B6"/>
<text x="49" y="100" font-family="Segoe UI, Roboto, Helvetica, Arial, sans-serif" font-size="12.5" font-weight="700" fill="#FFFFFF">Common.Lines</text>
<rect x="41" y="127" width="198" height="18" fill="#EEF3FA"/>
<text x="49" y="122" font-family="Consolas, Courier New, monospace" font-size="10.6" font-weight="700" fill="#1A1A1A">Pk</text>
<text x="231" y="122" font-family="Consolas, Courier New, monospace" font-size="9.4" text-anchor="end" fill="#767676">uniqueidentifier</text>
<text x="49" y="140" font-family="Consolas, Courier New, monospace" font-size="10.6" fill="#1A1A1A">Name</text>
<text x="231" y="140" font-family="Consolas, Courier New, monospace" font-size="9.4" text-anchor="end" fill="#767676">varchar(100)</text>
<text x="49" y="158" font-family="Consolas, Courier New, monospace" font-size="10.6" font-weight="700" fill="#1A1A1A">Fk_Plant</text>
<text x="231" y="158" font-family="Consolas, Courier New, monospace" font-size="9.4" text-anchor="end" fill="#767676">uniqueidentifier</text>

<!-- Relación uno-a-muchos hacia otra tabla a la derecha: línea + pata de gallo en el extremo «muchos» -->
<path d="M240 121 L300 121 L300 200 L389 200" stroke="#5B7DB1" stroke-width="1.3" fill="none"/>
<path d="M400 200 L391 194 M400 200 L391 200 M400 200 L391 206" stroke="#5B7DB1" stroke-width="1.3" fill="none"/>
</svg>
```

Geometría útil: alto de cabecera 29px, alto de fila 18px, alto de caja = 29 + 18 × filas, texto de
fila en `y = topFila + 13`. Separa las cajas al menos 60px para que las líneas tengan sitio.

**Nada puede salirse del lienzo.** Deja 40px de margen en los cuatro lados y comprueba que ninguna
`x` ni `y` (línea, punta de flecha o etiqueta) caiga fuera del `viewBox`: la captura recorta el
`<svg>` exacto, así que lo que se sale desaparece a medias y se nota. El caso que más falla es la
**relación de una tabla consigo misma** (`Fk_Parent`), porque el bucle tiende a dibujarse hacia
fuera. Sácalo por un lado con sitio libre y vuelve a entrar por el mismo lado:
`M x1 y1 L x1-30 y1 L x1-30 y2 L x1 y2`, con la etiqueta dentro del lienzo.

## Diagrama de arquitectura / componentes

Cajas rectangulares `rx="6"` con relleno claro (`#EEF3FA` para capas de aplicación, `#FFF7E6` para
infraestructura, `#F3F4F6` para externos), borde `#2E75B6`, etiqueta centrada (`text-anchor="middle"`)
en 13px `font-weight="600"`. Flechas `#5B7DB1` con cabeza `<path>` triangular; el sentido de la
flecha es **quién referencia a quién**. Agrupa capas en franjas horizontales con una etiqueta a la
izquierda en `#767676`.

## Respaldo: arte ASCII

Para una secuencia simple, o cuando no hay rasterizador, un `<pre><code>` con arte ASCII se guarda
sin problemas y se lee en el PDF:

```html
<pre style="background: rgba(243, 244, 246, 1); border-radius: 6px; padding: 12px; overflow-x: auto"><code>Host ──► AlmacenLite.Api ◄── Extensión
             │
             └──► AlmacenLite.DL ──► SQL Server</code></pre>
```
