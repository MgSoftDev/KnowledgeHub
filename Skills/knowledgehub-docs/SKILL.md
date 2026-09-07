---
name: knowledgehub-docs
description: >
  Genera o actualiza documentación de aplicaciones (manuales de usuario, referencia técnica de
  módulos y plugins, tutoriales paso a paso, notas de versión, instalación, arquitectura y modelo de
  datos con diagramas ER) como fragmentos HTML listos para pegar en el editor de
  MgSoftDev.KnowledgeHub: HTML que sobrevive a su saneador, imágenes embebidas en base64, diagramas
  dibujados en SVG y rasterizados a PNG, y el estilo de documentación de Fers (MG Soft AI), que es
  español de usted, tablas figure, callouts del editor y Preguntas Frecuentes al cierre.
  USA SIEMPRE esta skill cuando el usuario pida escribir, redactar, generar o ACTUALIZAR
  documentación de una app, un módulo, un plugin, una pantalla, un catálogo, una base de datos, una
  instalación o una versión. También cuando mencione «manual», «documentación», «documentar»,
  «HTML para el editor», «pegar en KnowledgeHub», «portal de documentación», «base de conocimiento»,
  «wiki interna», «notas de versión», «diagrama ER» o «modelo de datos». Y también cuando pida un
  documento HTML sin decir para qué, si nombra una carpeta de documentos o archivos con nombres
  del estilo NN-Nombre.html, o si el proyecto ya tiene una carpeta así.
  NO la uses para responder preguntas sobre cómo funciona la librería KnowledgeHub por dentro, para
  arreglar su código, para un README de repositorio ni para comentarios XML de C#.
---

# Documentación para KnowledgeHub — Skill de uso

KnowledgeHub es un módulo de documentación colaborativa con un editor HTML. Esta skill produce lo
que ese editor acepta: **fragmentos HTML** que el autor copia y pega, y que sobreviven intactos al
guardado. Esa última parte es la que justifica la skill: el saneador de KnowledgeHub borra en
silencio todo lo que no está en sus listas, y **rechaza el guardado entero** si encuentra un SVG
embebido. Un documento generado sin estas reglas se rompe al pegar, sin ningún aviso.

## Cuándo aplicar

- Documentar una aplicación, módulo, plugin, pantalla, esquema de base de datos, versión o
  instalación **para leerse en KnowledgeHub**, a partir del código, de una descripción o de ambos.
- Convertir documentación existente (Markdown, Word, wiki) al formato del editor.
- Dibujar el diagrama ER o de arquitectura que acompaña a esa documentación.

**No aplica** para: modificar el código de la propia librería KnowledgeHub (eso es trabajo sobre su
repo, no sobre documentación), un README de repositorio o la documentación XML de código C#.

## Referencias y ejemplos

Lee lo que corresponda **antes** de escribir la primera línea de HTML:

| Archivo | Cuándo leerlo |
|---|---|
| `references/tipos-de-documento.md` | Siempre: decide el tipo y te da su esqueleto de secciones |
| `references/plantillas.md` | Siempre: los bloques exactos (callouts, tablas, código, imágenes, FAQ, portada) |
| `references/html-permitido.md` | Cuando dudes de una etiqueta, atributo, CSS o URL. Es la tabla verificada del saneador |
| `references/diagramas.md` | Si el documento lleva diagrama ER o de arquitectura |
| `examples/` | Cuatro documentos de una app ficticia con el estilo exacto: portada, manual, referencia de módulo, modelo de datos con su `diagramas/*.svg` |

Scripts (Python 3, en `scripts/`, con Playwright y Pillow para `embed_images.py` y `svg_to_png.py`):

| Script | Para qué |
|---|---|
| `validate_html.py *.html --allow-class-prefix kh- --convenciones` | Linter: reproduce las reglas del saneador y las convenciones. Cero errores antes de entregar |
| `embed_images.py archivo.html` | Sustituye cada `<img src="archivo">` por base64, y los `.svg` los rasteriza a PNG antes |
| `svg_to_png.py diagramas/` | Rasteriza SVG → PNG (2x). `embed_images.py` lo llama solo. Úsalo suelto para revisar un diagrama |

---

## 1. Flujo de trabajo

1. **Entiende la app antes de escribir.** Lee el código, las pantallas, la base de datos o lo que
   haya, y pregunta al usuario lo que no esté (para quién es, qué versión, qué carpeta de destino).
   Decide el **tipo de documento** con `tipos-de-documento.md`. Si un tema mezcla usuario final y
   configuración técnica, son dos documentos. **La versión que cites sale del proyecto**
   (`Directory.Build.props`, el `.csproj`, `AssemblyInfo`, el último tag o el changelog), nunca de
   tu memoria. Si no aparece y no puedes preguntar, escribe «[VERSIÓN]» en el bloque de Consejos y
   ponlo en la lista de pendientes: un hueco se revisa, una versión inventada se publica.
2. **Escribe cada documento en un archivo** `NN-NombreEnPascal.html`, UTF-8 sin BOM, en la carpeta
   que diga el usuario (por defecto `Documentos/` junto al proyecto). Nunca lo pegues en el chat:
   son archivos que se copian al editor, y con imágenes en base64 pesan.
3. **Diagramas**: dibuja el SVG en `diagramas/nombre.svg` siguiendo `diagramas.md`, referéncialo con
   `<img src="diagramas/nombre.svg" style="max-width: 100%">` y ejecuta
   `python scripts/embed_images.py archivo.html`. El PNG queda dentro del HTML y el SVG se conserva
   como fuente.
4. **Capturas de pantalla**: no puedes tomarlas. Deja el marcador visible de `plantillas.md`
   (`<p style="…dashed…">[IMAGEN: qué debe verse]</p>`) exactamente donde va, con descripción
   suficiente para que el autor la tome sin releer el texto. No uses un comentario HTML: el saneador
   los borra al guardar y la imagen que faltaba se olvida.
5. **Valida**: `python scripts/validate_html.py Documentos/*.html --allow-class-prefix kh- --convenciones`.
   Corrige hasta cero errores y lee los avisos, que suelen ser reales. La línea `I-ENLACES` de cada
   archivo no es un problema: es la lista de enlaces relativos con sus líneas, lista para copiar.
6. **Cierra con la lista de pendientes**, en el chat: cada `[IMAGEN: …]` con su archivo, y cada
   enlace `href="NN-…html"` (tómalos de las líneas `I-ENLACES`) que el autor tendrá que sustituir en
   KnowledgeHub (clic derecho sobre la página en el árbol → **Copiar enlace**).

## 2. Las reglas que no se negocian, y por qué

- **Fragmento, no documento.** Empieza en `<h1>Título</h1>` y no lleva `<html>`, `<head>`,
  `<body>`, `<style>`, `<script>` ni `<meta>`. El `<h1>` es el título de la página en KnowledgeHub y
  además hace que el PDF no imprima el suyo encima.
- **Estilo inline, clases solo `kh-*`.** El saneador borra toda clase que no esté registrada (de
  fábrica sólo `kh-callout`, y el anfitrión de referencia permite el prefijo `kh-`). No hay hoja de
  estilos: lo que no vaya en `style="…"` de cada elemento no existe.
- **Sin `id`, sin `data-*`, sin `aria-*`.** Se borran siempre. Las anclas del índice de la página
  las genera KnowledgeHub al mostrarla, así que no las escribas.
- **Imágenes en base64 (PNG, JPEG, WebP). Jamás SVG, jamás archivos.** Un `data:image/svg+xml`
  rechaza el guardado entero. Un `<svg>` inline se borra con su contenido, y un `src="captura.png"`
  queda apuntando a un archivo que KnowledgeHub no tiene. Al guardar, cada base64 se sube como
  imagen (reducida a 1600 px de ancho y recomprimida a WebP), así que embeber es lo correcto.
- **Enlaces relativos entre hermanos** (`href="03-Cambio.html"`), sin anclas ni `mailto:`. Los
  `mailto:` pierden el `href` y las anclas no tienen destino. Los relativos se sustituyen en
  KnowledgeHub con «Copiar enlace», y por eso se listan al final.
- **Escapa `<`, `>` y `&` dentro de `<pre><code>`.** Un `<Project>` sin escapar es una etiqueta para
  el parser, no texto, y desaparece.
- **Cada documento termina en «Preguntas Frecuentes»** (salvo la portada), y los manuales llevan
  antes «Errores Comunes» en tabla. Es lo que el lector busca primero.

Todo lo anterior lo comprueba `validate_html.py`. La tabla completa, con lo que cada cosa hace al
guardar, está en `html-permitido.md`.

## 3. Estilo

- **Español, usted, presione.** Documentación de planta y de oficina: «presione», «seleccione»,
  «capture», nunca «haga clic» ni «tú». Sin emojis en la prosa (sólo los de los callouts).
- **Casi sin punto y coma.** En el español de México el «;» es raro: los documentos de referencia
  llevan uno o dos por cada mil palabras, y una IA sin esta regla escribe ocho. Cuando te salga uno,
  parte la frase en dos («… exporta solo esa página. El desplegable ofrece …»), o une con coma y
  conector («…, y el desplegable …», «…, así que …»), o usa dos puntos si lo segundo explica lo
  primero. Dentro de `<code>` y de los `style` el «;» es sintaxis y no cuenta. El validador avisa
  (`A-PUNTOYCOMA`) al pasar de cinco por cada mil palabras.
- **Title Case en los encabezados**: `Las Cuatro Capas`, `Cómo Descubre la Aplicación sus
  Extensiones`. Artículos y preposiciones en minúscula.
- **`<strong>` para cada nombre de cosa** (botón, menú, tabla, proyecto, columna, módulo), en su
  idioma original y sin traducir. `<em>` para citar literales de pantalla. `<code>` para GUIDs,
  valores y rutas.
- **Explica el porqué**, no sólo el qué: «comparte la región con X, y su orden relativo no está
  definido» vale más que «puede aparecer encimado». Frases de contraste cuando ayuden: «no es una
  aplicación monolítica: es un host de complementos».
- **Tablas para lo que se consulta** (síntoma/causa, columna/tipo, tabla/qué es). **Prosa para lo
  que se entiende**. Sin celdas vacías: «Ninguna» o «—».
- **Aire entre secciones con `margin-top`** (`<h2 style="margin-top: 1.6em">`), no con encabezados
  vacíos. Un `<h2><br></h2>` ensucia el PDF y el índice de la página.
- **Un callout por sección como mucho**: Nota (contexto), Advertencia (puede salir mal),
  Importante (no es opcional). Un documento lleno de cajas rojas no avisa de nada.
- Prosa envuelta a ~100 columnas. Tablas, callouts y bloques de código en una sola línea física
  (salvo el contenido del `<code>`).

## 4. Diagramas

**Cuándo dibujar uno.** Cuando el tipo de documento lo lleva (modelo de datos, arquitectura) o el
usuario lo pide. Un manual de usuario o una referencia de módulo no llevan diagrama por defecto: cada
uno añade cientos de KB al HTML y solo se justifica si aclara algo que la prosa y una tabla no pueden.

La fuente es SVG (precisión, editable) y lo embebido es PNG (lo único que KnowledgeHub acepta). Las
convenciones de dibujo —cajas de tabla azules, zebra, pata de gallo, fuentes— están en
`diagramas.md`, con una plantilla y el ER de `examples/diagramas/er-ejemplo.svg` para copiar. Sin
rasterizador disponible, el marcador visible `[IMAGEN: diagrama diagramas/x.svg pendiente de
convertir a PNG]`, nunca el SVG embebido.

## 5. Errores comunes

- **Marcar imágenes pendientes con `<!-- comentarios -->`.** El saneador los borra al guardar y
  nadie vuelve a saber qué captura faltaba. El marcador es un párrafo visible.
- **Pegar un `<svg>` en el HTML o un `data:image/svg+xml`.** El primero desaparece, el segundo
  bloquea el guardado con un mensaje que el autor no esperaba. Rasteriza con `embed_images.py`.
- **Poner `id` a los encabezados para enlazar secciones.** Se borra al guardar y el enlace queda
  muerto. KnowledgeHub genera su propio índice de secciones, y entre documentos se enlaza al archivo.
- **Inventar clases (`class="nota"`, `class="tabla-compacta"`).** Se borran y el elemento queda sin
  estilo. Todo va inline, y las cajas son las de `plantillas.md`.
- **Escribir `<h2><br></h2>` para separar** como hacía el editor visual. Usa `margin-top`.
- **Documento HTML completo** con `<html><head><style>`. El estilo de `<style>` se pierde entero y
  el fragmento se extrae mal.
- **Un `<pre><code>` con XML o C# sin escapar `<`.** El contenido se convierte en etiquetas y se
  pierde. Escapa siempre.
- **Mezclar público**: un manual de operador con el SQL de registro. Son dos documentos en dos
  carpetas, enlazados desde sus portadas.
- **Dar el trabajo por terminado sin `validate_html.py` a cero errores** y sin la lista de imágenes
  y enlaces pendientes. El autor descubre lo que falta al pegar, que es el peor momento.
