# HTML que sobrevive al guardado en KnowledgeHub

Todo lo que se pega en el editor pasa por el saneador **al guardar**, siempre en su nivel
Standard (Ganss.Xss HtmlSanitizer 9.1.x con la configuración del paquete
`MgSoftDev.KnowledgeHub.HtmlSanitizer`). Lo que no está en sus listas **desaparece sin aviso**:
no hay error, no hay log visible para el autor. Esta tabla sale de ejecutar ese saneador, no de
leer su documentación. Cuando dudes, `scripts/validate_html.py` la aplica por ti.

## Etiquetas

**Permitidas** (las 99 de fábrica; KnowledgeHub no añade ninguna):

```
a abbr acronym address area article aside b bdi big blockquote br button caption center cite
code col colgroup data datalist dd del details dfn dir div dl dt em fieldset figcaption figure
font footer form h1 h2 h3 h4 h5 h6 header hr i img input ins kbd label legend li main map mark
menu meter nav ol optgroup option output p pre progress q rp rt ruby s samp section select small
span strike strong sub summary sup table tbody td textarea tfoot th thead time tr tt u ul var wbr
```

**Prohibidas — se borran CON su contenido** (`KeepChildNodes = false` en Standard):

| Etiqueta | Qué pasa | Alternativa |
|---|---|---|
| `svg`, `path`, `g`, `circle`, `rect`, `text` (SVG inline) | Desaparece el diagrama entero | Rasterizar a PNG y embeber en base64 (`scripts/embed_images.py`) |
| `iframe`, `video`, `audio`, `embed`, `object`, `canvas`, `picture`, `source` | Se borran | Una captura estática, o un enlace |
| `script`, `style`, `link`, `meta`, `title`, `noscript`, `template` | Se borran | Estilo **inline** en cada elemento |
| `html`, `head`, `body`, `<!DOCTYPE>` | El fragmento se extrae, no los escribas | Empieza directamente con `<h1>` |
| `<!-- comentarios -->` | **Se borran** al guardar, sin rastro | Para marcar una imagen pendiente, un párrafo visible (ver `plantillas.md`) |

## Atributos

**Permitidos**: `style`, `title`, `alt`, `src`, `href`, `target`, `rel`, `lang`, `dir`, `name`,
`open` (details), `width`, `height`, `colspan`, `rowspan`, `align`, `border`, `cellpadding`,
`cellspacing`, `valign`, `nowrap`, `bgcolor`, `span`, `scope`, `type`, `start`, `reversed`,
`datetime`, `cite`, `abbr`, `headers`, `summary`. Y `class`, con la trampa de abajo.

**Prohibidos — se borran (el elemento queda, el atributo no)**:

| Atributo | Consecuencia |
|---|---|
| `id` | Se borra **siempre**, en los tres niveles. Las anclas del panel «En esta página» las genera KnowledgeHub al mostrar la página, así que no hacen falta; y un `href="#mi-ancla"` propio nunca tendrá destino |
| `data-*` | Se borran (`AllowDataAttributes` está apagado) |
| `aria-*`, `role` | Se borran (no hay comodines en la lista) |
| `face` (en `font`), `download`, `srcset`, `sizes`, `loading` | Se borran |

### `class`: solo lo registrado, y en minúsculas exactas

De fábrica sobrevive **una sola clase**: `kh-callout`. El anfitrión puede permitir más
(`AllowedClasses` / `AllowedClassPrefixes` al registrar el saneador). El anfitrión de referencia
permite el prefijo `kh-`, y de ahí salen `kh-module-index`, `kh-mi-card`, `kh-tech`. La comparación
es **case-sensitive**: `KH-CALLOUT` se borra. Cualquier otra clase (`MsoNormal`, `note`, `table`)
se elimina del atributo, y si no queda ninguna, el atributo desaparece.

Regla práctica: **cero clases propias**. Todo el aspecto va en `style`. Las `kh-*` son las del
paquete y las que el anfitrión haya aceptado, nada más.

## CSS dentro de `style`

Permitidas las 239 propiedades de fábrica más `zoom`. Las que importan en documentación:

| Propiedad | Standard (guardar) | Strict (escoba nivel 2) |
|---|---|---|
| `color`, `background`, `background-color` | sí | **no** |
| `border`, `border-left`, `border-radius`, `padding`, `margin` | sí | sí |
| `font-weight`, `font-style`, `font-size`, `font-family`, `text-decoration` | sí | **no** |
| `text-align`, `vertical-align`, `width`, `max-width`, `height`, `display`, `float`, `list-style` | sí | sí |
| `white-space`, `overflow`, `overflow-x`, `line-height` | sí | **no** |

Tres cosas que muerden:

- **Un valor que el parser no entiende tumba la propiedad entera.** `word-break: break-all` pasa;
  `word-break: break-word` (obsoleto) desaparece. Usa valores estándar y conservadores.
- **Custom properties (`--x: …`) se borran, pero `var(--x)` se queda** apuntando a nada. Nunca las
  declares inline.
- **Todo se normaliza al guardar**: `#3b82f6` → `rgba(59, 130, 246, 1)`, `border: 1px solid #ccc`
  → tres longhands + `border-left`, se quita el `;` final. Es cosmético e idempotente: escribe hex y
  shorthands tranquilo, pero no esperes que el HTML guardado sea byte a byte el tuyo.

## URLs (`href`, `src`)

| Forma | Sobrevive |
|---|---|
| `https://…`, `http://…` | sí |
| Ruta relativa (`03-Cambio.html`, `/kh/page/{guid}`) y `#ancla` | sí |
| `data:image/png;base64,…` (y jpeg, gif, webp) | sí — y **al guardar se sube como imagen** (ver abajo) |
| `docimg://{guid}` | sí (es lo que KnowledgeHub deja tras subir una imagen) |
| `mailto:`, `tel:` | **no**: el `<a>` queda, sin `href`. Un correo va como texto o `<code>` |
| `javascript:` | no |

## Imágenes: el pipeline de guardado

Cada `<img src="data:image/…;base64,…">` se **sube** al guardar: se decodifica, se reduce a un
ancho máximo de 1600 px si lo supera (sin tope de alto), se recomprime **siempre a WebP**, se
deduplica por hash y el `src` se sustituye por `docimg://{guid}`. No hay límite de bytes, pero un
HTML de varios MB es incómodo de pegar: reduce las capturas antes de embeberlas.

Formatos que decodifica: PNG, JPEG, GIF, BMP, WebP, TIFF, TGA, PBM, QOI.

**SVG no está entre ellos. Un `data:image/svg+xml;base64,…` RECHAZA EL GUARDADO ENTERO** con el
mensaje *«No se pudieron procesar N imagen(es) pegada(s): image/svg+xml. Quítalas o conviértelas a
PNG/JPG antes de guardar»*. Por eso los diagramas se dibujan en SVG **como fuente** y se embeben
**como PNG** (`scripts/svg_to_png.py` / `scripts/embed_images.py`). La variante URL-encoded
(`data:image/svg+xml,%3Csvg…`) es peor: no se rechaza, se queda inline en la base de datos para
siempre. No la emitas nunca.

Un `src` a archivo (`img/captura.png`, `https://…/foto.png`) sobrevive al saneador pero **no se
sube**: queda apuntando a un archivo que KnowledgeHub no tiene. Embebe siempre.

## Plantillas Scriban (`{{ … }}`)

En una página normal (sin marcar «Rellenar con datos en vivo»), `{{ … }}` es texto y se conserva.
Dos excepciones que el saneador aplica igual:

- `{{ }}` con `<`, `>` o `&&` dentro se **escapa** (`&lt;`, `&amp;&amp;`).
- `{{ }}` dentro de `<table>` **fuera de una celda** se **expulsa** de la tabla (reglas de parseo
  de tablas de HTML). Solo dentro de `<td>`/`<th>`.

Si documentas una app que usa llaves (Angular, Handlebars, Scriban), esas dos reglas te afectan.

## Lo que hace la escoba del editor (por si el autor la usa después)

- **Nivel 2 (Strict)**: quita `color`, `bgcolor`, `face`, `size`, `align`; desenvuelve `font` y
  `span` (el texto se queda). Deja solo CSS estructural (márgenes, bordes, tamaños, `display`,
  `text-align`). **`<img>` y `.kh-callout` conservan todo su `style`.**
- **Nivel 3 (solo texto)**: solo `p`, `br`, `img` con `src`, `alt`, `style` de tamaño. Se pierden
  encabezados, tablas, código, callouts y **enlaces** (queda el texto). No lo uses sobre un documento
  con referencias cruzadas.

## Lo que KnowledgeHub hace con el documento después

- **Panel «En esta página»**: lista los `h1`–`h3` con texto (hasta el nivel que configure el
  anfitrión) y les inyecta anclas al mostrar. Un encabezado vacío no aparece.
- **PDF**: el exportador pone el título de la página como encabezado **salvo que el contenido ya
  tenga un `<h1>` con texto**, en cuyo caso calla. Empezar por `<h1>` evita el título duplicado.
- **Página vacía**: un documento sin texto y sin imagen se excluye del PDF. `<p><br></p>` no cuenta
  como contenido.
