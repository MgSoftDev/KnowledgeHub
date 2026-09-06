r"""
validate_html.py — Linter de fragmentos HTML para MgSoftDev.KnowledgeHub.

Comprueba que un fragmento sobrevivirá al saneador de KnowledgeHub y sigue las convenciones
de la skill `knowledgehub-docs`. No modifica nada. Las listas de etiquetas, atributos y
propiedades CSS están copiadas de la configuración de fábrica de HtmlSanitizer (Ganss.Xss
9.1.968-beta, leídas del ensamblado) más lo que añade KnowledgeHubSanitizerDefaults:
esquemas `data` y `docimg`, atributo `class` limitado a `kh-callout`, propiedad CSS `zoom`.

CÓMO LIMPIA KNOWLEDGEHUB (lo que hay que tener en la cabeza)
  * Guardar sanea SIEMPRE al nivel Standard: etiqueta no permitida → se borra CON su
    contenido (KeepChildNodes=false); atributo no permitido → se borra; propiedad CSS no
    permitida → se borra; esquema de URL no permitido → el href/src se borra (la etiqueta queda).
  * Las imágenes viajan como data:image/...;base64 y al guardar se convierten a WebP y a
    docimg://{pk}. Un data:image/svg+xml RECHAZA el guardado entero.
  * Los {{ }} de Scriban solo se protegen si la página tiene «Usa plantillas» activo; si no,
    son texto normal y se escapan como cualquier otro.
  * Los comentarios HTML (<!-- -->) se eliminan al guardar: no sirven como marcador.

SEVERIDADES
  ERROR  → el saneador destruye o rechaza algo: hay que corregir (salida 1).
  AVISO  → sobrevive, pero probablemente no hace lo que el autor cree.
  INFO   → métricas del documento.

REGLAS — ERROR
  E-FRAGMENTO   <html>, <head>, <body>, <!DOCTYPE>, <style>, <script>, <link>, <meta>,
                <title>, <base>: KnowledgeHub almacena el innerHTML del body de un fragmento.
                style/script/link/meta/title se eliminan enteros.
  E-H1          El fragmento debe empezar por un <h1> con texto: es el título del documento
                y el exportador a PDF omite su título sintético solo si encuentra un <h1>
                legible; sin él salen dos títulos.
  E-TAG         Etiqueta fuera de la allow-list (svg, path, g, iframe, video, audio, canvas,
                embed, object, picture, source, noscript, template... y cualquier otra
                desconocida): se borra con todo su contenido. Dentro de <pre>/<code> casi
                siempre es un '<' sin escapar.
  E-ATTR-ID     `id` no está en la allow-list y se borra en los tres niveles; las anclas
                del índice se generan al renderizar (kh-<slug>) y no se persisten.
  E-ATTR-DATA   data-*, aria-*, role: se borran (AllowDataAttributes=false).
  E-ATTR-ON     on*: se borra; y delata contenido interactivo que no funcionará.
  E-CLASS       class distinta de kh-callout (única clase de fábrica): se borra al guardar.
                --allow-class / --allow-class-prefix reflejan las KnowledgeHubSanitizerOptions
                del anfitrión (AllowedClasses / AllowedClassPrefixes).
  E-HREF        mailto:, tel:, javascript: (o cualquier esquema que no sea http/https/data/
                docimg): el <a> sobrevive SIN href.
  E-SRC-SVG     src="data:image/svg+xml...": rechaza el guardado entero.
  E-SRC         src externo (.png, .svg, http...): el editor no puede resolverlo y el
                saneador lo borra si el esquema no está permitido. Debe ser base64
                (embed_images.py) o docimg://.
  E-SRC-FORMA   data: que no casa con DataUriRegex (saltos de línea, ;charset, sin ;base64):
                no se sube y se queda inline.
  E-CSS-VAR     --x: en style (custom property) se borra (AllowCssCustomProperties=false) y
                todo var(--x) queda sin valor.
  E-CSS-PELIGRO expression(), javascript: o url() con esquema no permitido: se elimina.
  E-UTF8        El archivo no es UTF-8.

REGLAS — AVISO
  A-TABLA       Texto o elemento no tabular (típicamente {{ for }}) directamente dentro de
                <table>, <thead>, <tbody>, <tfoot> o <tr>: el parser HTML lo expulsa fuera de
                la tabla (foster parenting) y un bucle deja de envolver las filas. Solo se
                protege si la página usa plantillas.
  A-PLANTILLA   {{ ... }} con <, > o &&: se escapan al sanear (salvo con «Usa plantillas») y
                Scriban no tiene lt/gt textuales. También {{ y }} desbalanceados.
  A-PRE         <pre> sin <code>: la convención (y el CSS del PDF, `pre code`) es <pre><code>.
  A-CODE        <code> de bloque con < o > sin escapar: usa &lt; y &gt;.
  A-IMG-ANCHO   <img> de más de 900 px (ancho leído con Pillow del base64; sin Pillow, más
                de 300 KB) sin style con max-width/width: el lector la limita al 100 % por
                CSS (.kh-doc-content img), pero el editor y un anfitrión con CSS propio no.
  A-IMG-PESO    Imagen embebida de más de 2 MB.
  A-ANCLA       href="#algo": los id se borran y el router de Blazor intercepta los
                fragmentos; el lector ya ofrece el panel «En esta página».
  A-ATTR        Atributo fuera de la allow-list (loading, srcset...): se borra, sin más.
  A-CSS         Propiedad CSS fuera de la lista de fábrica (-webkit-*, aspect-ratio...): se borra.
  A-P-BLOQUE    Bloque (div, ul, table, h2...) dentro de <p>: el parser cierra el párrafo
                antes; la estructura no será la escrita.
  A-CIERRE      Etiquetas sin cerrar o cierres sin apertura.
  A-VACIO       Encabezados sin texto legible (una línea por archivo): no entran en el índice
                «En esta página». El <h2><br></h2> como separador es de la etapa anterior; hoy
                el aire va en style="margin-top" del encabezado siguiente.
  A-CDATA       Sección CDATA: se descarta.
  --convenciones añade: A-FAQ (la última <h2> debe ser «Preguntas Frecuentes»), A-H1-UNICO,
  A-SALTO-NIVEL (h3 sin h2 previo) y A-PUNTOYCOMA (más de 5 «;» por cada mil palabras de prosa,
  fuera de <pre>/<code>: en español de México se usa poco; los documentos de referencia llevan 1 o
  2). Una portada (rejilla kh-module-index) queda exenta de A-FAQ y A-SALTO-NIVEL: no lleva FAQ y
  agrupa con h3.

INFO
  I-ENLACES     La lista de href relativos (NN-Archivo.html) con sus líneas. Es el flujo
                previsto —se sustituyen en KnowledgeHub con clic derecho en el árbol →
                Copiar enlace— y esta línea es la lista de pendientes con la que se cierra.
  I-COMENTARIO  Hay comentarios HTML: se eliminan al guardar.
  Y el resumen: h1/h2/h3, imágenes (con peso), tablas, callouts (div.kh-callout), enlaces y
  peso del archivo.

USO
  python validate_html.py build/manual.html
  python validate_html.py build/*.html --convenciones
  python validate_html.py build/ --recursive --allow-class-prefix kh- --json
  Salida 0 sin errores · 1 con algún ERROR (o AVISO con --strict) · 2 error de uso.
"""
from __future__ import annotations

import argparse
import base64
import glob
import html as html_lib
import io
import json
import re
import sys
import unicodedata
from dataclasses import asdict, dataclass, field
from html.parser import HTMLParser
from pathlib import Path

# ----------------------------------------------------------------------- listas de fábrica (Ganss.Xss 9.1.968-beta)

ALLOWED_TAGS = frozenset("""
a abbr acronym address area b big blockquote br button caption center cite code col colgroup dd del dfn dir div dl dt
em fieldset font form h1 h2 h3 h4 h5 h6 hr i img input ins kbd label legend li map menu ol optgroup option p pre q s
samp select small span strike strong sub sup table tbody td textarea tfoot th thead tr tt u ul var
section nav article aside header footer main figure figcaption data time mark ruby rt rp bdi wbr
datalist keygen output progress meter details summary menuitem
""".split())
# html, head y body también están en la lista de fábrica, pero aquí caen por E-FRAGMENTO.

STRUCTURAL_TAGS = frozenset("html head body style script link meta title base".split())
EXPLICIT_FORBIDDEN = frozenset("svg path g iframe video audio canvas embed object picture source noscript template".split())

ALLOWED_ATTRIBUTES = frozenset("""
abbr accept accept-charset accesskey action align alt axis bgcolor border cellpadding cellspacing char charoff charset
checked cite clear cols colspan color compact coords datetime dir disabled enctype for frame headers height href
hreflang hspace ismap label lang longdesc maxlength media method multiple name nohref noshade nowrap prompt readonly
rel rev rows rowspan rules scope selected shape size span src start style summary tabindex target title type usemap
valign value vspace width high keytype list low max min novalidate open optimum pattern placeholder pubdate
radiogroup required reversed spellcheck step wrap challenge contenteditable draggable dropzone autocomplete autosave
class
""".split())   # `class` lo añade KnowledgeHubSanitizerDefaults

ALLOWED_CSS = frozenset("""
align-content align-items align-self all animation animation-delay animation-direction animation-duration
animation-fill-mode animation-iteration-count animation-name animation-play-state animation-timing-function
backface-visibility background background-attachment background-blend-mode background-clip background-color
background-image background-origin background-position background-position-x background-position-y
background-repeat background-repeat-x background-repeat-y background-size border border-bottom border-bottom-color
border-bottom-left-radius border-bottom-right-radius border-bottom-style border-bottom-width border-collapse
border-color border-image border-image-outset border-image-repeat border-image-slice border-image-source
border-image-width border-left border-left-color border-left-style border-left-width border-radius border-right
border-right-color border-right-style border-right-width border-spacing border-style border-top border-top-color
border-top-left-radius border-top-right-radius border-top-style border-top-width border-width bottom
box-decoration-break box-shadow box-sizing break-after break-before break-inside caption-side caret-color clear clip
color column-count column-fill column-gap column-rule column-rule-color column-rule-style column-rule-width
column-span column-width columns content counter-increment counter-reset cursor direction display empty-cells
filter flex flex-basis flex-direction flex-flow flex-grow flex-shrink flex-wrap float font font-family
font-feature-settings font-kerning font-language-override font-size font-size-adjust font-stretch font-style
font-synthesis font-variant font-variant-alternates font-variant-caps font-variant-east-asian
font-variant-ligatures font-variant-numeric font-variant-position font-weight gap grid grid-area grid-auto-columns
grid-auto-flow grid-auto-rows grid-column grid-column-end grid-column-gap grid-column-start grid-gap grid-row
grid-row-end grid-row-gap grid-row-start grid-template grid-template-areas grid-template-columns grid-template-rows
hanging-punctuation height hyphens image-rendering isolation justify-content left letter-spacing line-break
line-height list-style list-style-image list-style-position list-style-type margin margin-bottom margin-left
margin-right margin-top mask mask-clip mask-composite mask-image mask-mode mask-origin mask-position mask-repeat
mask-size mask-type max-height max-width min-height min-width mix-blend-mode object-fit object-position opacity
order orphans outline outline-color outline-offset outline-style outline-width overflow overflow-wrap overflow-x
overflow-y padding padding-bottom padding-left padding-right padding-top page-break-after page-break-before
page-break-inside perspective perspective-origin pointer-events position quotes resize right row-gap
scroll-behavior tab-size table-layout text-align text-align-last text-combine-upright text-decoration
text-decoration-color text-decoration-line text-decoration-skip text-decoration-style text-indent text-justify
text-orientation text-overflow text-shadow text-transform text-underline-position top transform transform-origin
transform-style transition transition-delay transition-duration transition-property transition-timing-function
unicode-bidi user-select vertical-align visibility white-space widows width word-break word-spacing word-wrap
writing-mode z-index
zoom
""".split())   # `zoom` lo añade KnowledgeHubSanitizerDefaults (herramienta de tamaño de imagen)

ALLOWED_SCHEMES = frozenset("http https data docimg".split())
VOID_TAGS = frozenset("area base br col embed hr img input link meta param source track wbr".split())
P_CLOSERS = frozenset("""address article aside blockquote details dialog div dl fieldset figcaption figure footer
form h1 h2 h3 h4 h5 h6 header hr main menu nav ol p pre section table ul""".split())
OPTIONAL_END = frozenset("li p dt dd tr td th option optgroup thead tbody tfoot colgroup caption rt rp".split())
AUTO_CLOSE = {  # etiqueta que se abre → etiquetas abiertas en la cima que el parser cierra solas
    "li": {"li"}, "dt": {"dt", "dd"}, "dd": {"dt", "dd"}, "option": {"option"},
    "td": {"td", "th"}, "th": {"td", "th"}, "tr": {"td", "th", "tr"},
}
CELL_TAGS = frozenset("td th caption".split())
TABLE_PARTS = frozenset("table thead tbody tfoot tr".split())
TABLE_CHILDREN = frozenset("caption colgroup col thead tbody tfoot tr td th".split())
HEADINGS = ("h1", "h2", "h3", "h4", "h5", "h6")
CALLOUT_CLASS = "kh-callout"
INDEX_CLASS = "kh-module-index"    # rejilla de portada: sin FAQ ni jerarquía h2/h3
IMG_WIDE_PX = 900                 # .kh-doc-content { max-width: 900px } en knowledgehub.css
IMG_HEAVY_NO_PIL = 300 * 1024
IMG_HEAVY = 2 * 1024 * 1024
FAQ_TITLE = "preguntas frecuentes"

DATA_URI_RE = re.compile(r"^data:image/[a-zA-Z0-9.+-]+;base64,[A-Za-z0-9+/=]+$")   # KnowledgeHubHtml.DataUriRegex
SCHEME_RE = re.compile(r"^\s*([a-zA-Z][a-zA-Z0-9+.-]*):")
TEMPLATE_RE = re.compile(r"\{\{(.*?)\}\}", re.S)
URL_FUNC_RE = re.compile(r"""url\(\s*['"]?\s*([a-zA-Z][a-zA-Z0-9+.-]*):""", re.I)


# ----------------------------------------------------------------------- modelo

@dataclass
class Finding:
    severidad: str          # ERROR | AVISO | INFO
    linea: int
    columna: int
    codigo: str
    mensaje: str


@dataclass
class Stats:
    h1: int = 0
    h2: int = 0
    h3: int = 0
    imagenes: int = 0
    bytes_imagenes: int = 0
    tablas: int = 0
    callouts: int = 0
    enlaces: int = 0
    bytes_total: int = 0


def _configure_stdio() -> None:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass


def fmt_size(n: int) -> str:
    return f"{n / 1024:.1f} KB" if n < 1024 * 1024 else f"{n / (1024 * 1024):.2f} MB"


def normalize_ws(text: str) -> str:
    return " ".join(text.split())


def normalize_title(text: str) -> str:
    """Sin acentos, sin mayúsculas, sin puntuación final: «Preguntas Frecuentes:» == «preguntas frecuentes»."""
    decomposed = unicodedata.normalize("NFD", text)
    plain = "".join(ch for ch in decomposed if unicodedata.category(ch) != "Mn")
    return normalize_ws(plain).casefold().rstrip(":.?! ")


def split_declarations(style: str) -> list[tuple[str, str]]:
    """Declaraciones de un style="" respetando los ';' dentro de paréntesis (url(data:...;base64,...))."""
    declarations: list[str] = []
    depth = 0
    buffer: list[str] = []
    for ch in style:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth = max(0, depth - 1)
        if ch == ";" and depth == 0:
            declarations.append("".join(buffer))
            buffer = []
        else:
            buffer.append(ch)
    if buffer:
        declarations.append("".join(buffer))
    result: list[tuple[str, str]] = []
    for declaration in declarations:
        if ":" not in declaration:
            continue
        prop, _, value = declaration.partition(":")
        prop = prop.strip().lower()
        if prop:
            result.append((prop, value.replace("!important", "").strip()))
    return result


# ----------------------------------------------------------------------- recorrido del fragmento

class FragmentAuditor(HTMLParser):
    """Recorre etiquetas y atributos con html.parser; las comprobaciones de texto van aparte."""

    def __init__(self, allowed_classes: set[str], class_prefixes: list[str], extra_tags: set[str],
                 conventions: bool, use_pillow: bool) -> None:
        super().__init__(convert_charrefs=False)   # así un '<' escrito como &lt; no se confunde con uno crudo
        self.findings: list[Finding] = []
        self.stats = Stats()
        self.allowed_classes = allowed_classes
        self.class_prefixes = class_prefixes
        self.allowed_tags = ALLOWED_TAGS | extra_tags
        self.conventions = conventions
        self.use_pillow = use_pillow
        self.stack: list[tuple[str, int, int]] = []          # (etiqueta, línea, columna)
        self.first_tag_seen = False
        self.first_is_h1 = False
        self.headings: list[tuple[int, str, int]] = []      # (nivel, texto, línea)
        self._heading: list[str] | None = None
        self._heading_level = 0
        self._heading_pos = (0, 0)
        self.table_depth = 0
        self.cell_depth = 0
        self.pre_depth = 0
        self.code_depth = 0
        self._pre_frames: list[dict] = []
        self._autoclosed_p = 0
        self._comment_noted = False
        self.relative_links: list[tuple[str, int]] = []   # href relativos pendientes de «Copiar enlace»
        self.empty_headings: list[tuple[str, int]] = []   # encabezados sin texto, agregados al final
        self.is_index = False                              # portada con kh-module-index
        self.prose_words = 0                               # palabras fuera de <pre>/<code>
        self.semicolons: list[int] = []                    # línea de cada «;» en la prosa

    # ---- registro
    def add(self, severidad: str, codigo: str, mensaje: str, pos: tuple[int, int] | None = None) -> None:
        line, col = pos or self.getpos()
        self.findings.append(Finding(severidad, line, col, codigo, mensaje))

    def error(self, codigo: str, mensaje: str, pos: tuple[int, int] | None = None) -> None:
        self.add("ERROR", codigo, mensaje, pos)

    def warn(self, codigo: str, mensaje: str, pos: tuple[int, int] | None = None) -> None:
        self.add("AVISO", codigo, mensaje, pos)

    # ---- apertura
    def handle_starttag(self, tag, attrs):
        self._open(tag, attrs, self_closing=False)

    def handle_startendtag(self, tag, attrs):
        self._open(tag, attrs, self_closing=True)

    def _open(self, tag: str, attrs, self_closing: bool) -> None:
        attrs_dict = {name.lower(): value for name, value in attrs}
        if not self.first_tag_seen:
            self.first_tag_seen = True
            self.first_is_h1 = tag == "h1"
            if not self.first_is_h1:
                self.error("E-H1", f"el fragmento empieza por <{tag}>; debe empezar por <h1>Título del documento</h1>")

        if tag in STRUCTURAL_TAGS:
            self.error("E-FRAGMENTO", f"<{tag}> no procede: el contenido debe ser un fragmento (lo que iría dentro "
                                      "de <body>), sin html/head/body/style/script/link/meta/title")
        elif tag not in self.allowed_tags:
            if tag in EXPLICIT_FORBIDDEN:
                extra = " (rasteriza el SVG con svg_to_png.py o embed_images.py y embébelo como PNG)" \
                    if tag in ("svg", "path", "g") else ""
                self.error("E-TAG", f"<{tag}> no está permitido: el saneador lo borra CON todo su contenido{extra}")
            elif self.pre_depth > 0 or self.code_depth > 0:
                self.error("E-TAG", f"<{tag}> dentro de código: casi seguro un '<' sin escapar; escribe &lt;{tag}&gt;. "
                                    "El saneador borraría el elemento y todo lo que abarque")
            else:
                self.error("E-TAG", f"<{tag}> no está en la allow-list de fábrica de Ganss.Xss: se borra con su contenido")

        top = self.stack[-1][0] if self.stack else ""
        if tag in P_CLOSERS and top == "p":
            self.warn("A-P-BLOQUE", f"<{tag}> dentro de <p>: el parser cierra el párrafo antes; la estructura no será la escrita")
            self.stack.pop()
            self._autoclosed_p += 1
            self._on_close("p")
            top = self.stack[-1][0] if self.stack else ""
        if tag in AUTO_CLOSE and top in AUTO_CLOSE[tag]:
            self.stack.pop()
            self._on_close(top)
            top = self.stack[-1][0] if self.stack else ""
        if self.table_depth > 0 and self.cell_depth == 0 and top in TABLE_PARTS and tag not in TABLE_CHILDREN:
            self.warn("A-TABLA", f"<{tag}> directamente dentro de <{top}> sin celda: el parser lo expulsa fuera de la tabla")

        self._check_attributes(tag, attrs_dict)
        self._track_open(tag, attrs_dict)
        if not (self_closing or tag in VOID_TAGS):
            self.stack.append((tag, *self.getpos()))

    def _check_attributes(self, tag: str, attrs: dict[str, str | None]) -> None:
        for name, value in attrs.items():
            shown = (value or "")[:50]
            if name == "id":
                self.error("E-ATTR-ID", f'id="{shown}" en <{tag}> se borra al guardar (no está en la allow-list en '
                                        "ningún nivel); las anclas del índice se generan al renderizar como kh-<slug>")
            elif name.startswith("data-"):
                self.error("E-ATTR-DATA", f"{name} en <{tag}> se borra (AllowDataAttributes=false)")
            elif name.startswith("aria-") or name == "role":
                self.error("E-ATTR-DATA", f"{name} en <{tag}> se borra: no está en la allow-list")
            elif name.startswith("on"):
                self.error("E-ATTR-ON", f"{name} en <{tag}> se borra, y delata contenido interactivo que no funcionará")
            elif name == "class":
                self._check_classes(tag, value or "")
            elif name == "style":
                self._check_style(tag, value or "")
            elif name == "href":
                self._check_href(value)
            elif name == "src":
                self._check_src(tag, value, attrs)
            elif name not in ALLOWED_ATTRIBUTES:
                self.warn("A-ATTR", f'{name}="{shown}" en <{tag}> se borra (atributo fuera de la allow-list)')

    def _check_classes(self, tag: str, value: str) -> None:
        for cls in value.split():
            if cls in self.allowed_classes or any(cls.startswith(p) for p in self.class_prefixes):
                if cls == CALLOUT_CLASS:
                    self.stats.callouts += 1
                elif cls == INDEX_CLASS:
                    self.is_index = True
                continue
            self.error("E-CLASS", f'class="{cls}" en <{tag}> se borra al guardar: de fábrica solo sobrevive kh-callout '
                                  "(usa --allow-class/--allow-class-prefix si el anfitrión la declara en KnowledgeHubSanitizerOptions)")

    def _check_style(self, tag: str, value: str) -> None:
        for prop, val in split_declarations(value):
            low = val.lower()
            if prop.startswith("--"):
                self.error("E-CSS-VAR", f"{prop} en <{tag}>: las custom properties se borran (AllowCssCustomProperties=false) "
                                        f"y todo var({prop}) queda sin valor; escribe el valor literal")
                continue
            if "var(" in low:
                self.error("E-CSS-VAR", f"{prop} en <{tag}> usa var(): la custom property se borra y el valor queda vacío; escribe el literal")
            if "expression(" in low or "javascript:" in low:
                self.error("E-CSS-PELIGRO", f"{prop} en <{tag}> contiene expression()/javascript: y se elimina")
            m = URL_FUNC_RE.search(val)
            if m and m.group(1).lower() not in ALLOWED_SCHEMES:
                self.error("E-CSS-PELIGRO", f"{prop} en <{tag}>: url() con esquema {m.group(1)}: no permitido; la declaración se borra")
            if prop not in ALLOWED_CSS:
                self.warn("A-CSS", f"{prop} en <{tag}> se borra (propiedad fuera de la lista de fábrica)")

    def _check_href(self, value: str | None) -> None:
        if not value or not value.strip():
            return
        v = value.strip()
        m = SCHEME_RE.match(v)
        if m:
            scheme = m.group(1).lower()
            if scheme in ("mailto", "tel", "javascript"):
                self.error("E-HREF", f'href="{v[:50]}": el esquema {scheme}: no está permitido; el <a> sobrevive SIN href '
                                     "(escribe el correo o teléfono como texto)")
            elif scheme not in ALLOWED_SCHEMES:
                self.error("E-HREF", f'href="{v[:50]}": esquema {scheme}: no permitido (solo http, https, data, docimg); el href se borra')
            else:
                self.stats.enlaces += 1
            return
        if v.startswith("#"):
            self.warn("A-ANCLA", f'href="{v[:50]}": los id se borran al guardar y el router de Blazor intercepta los '
                                 "fragmentos; el lector ya ofrece el panel «En esta página»")
            return
        if v.startswith("/kh/page/"):
            self.stats.enlaces += 1        # enlace entre páginas (Copiar enlace del menú del árbol)
            return
        self.stats.enlaces += 1
        self.relative_links.append((v, self.getpos()[0]))

    def _check_src(self, tag: str, value: str | None, attrs: dict[str, str | None]) -> None:
        if value is None or not value.strip():
            self.error("E-SRC", f"<{tag}> sin src")
            return
        v = value.strip()
        low = v.lower()
        if low.startswith("data:image/svg+xml"):
            self.error("E-SRC-SVG", 'src="data:image/svg+xml...": ImageSharp no decodifica SVG y el guardado se RECHAZA '
                                    "entero; rasteriza con svg_to_png.py o embed_images.py")
        elif low.startswith("data:"):
            if not DATA_URI_RE.match(v):
                self.error("E-SRC-FORMA", "data: que no casa con DataUriRegex de KnowledgeHub (debe ser exactamente "
                                          "data:image/<tipo>;base64,<base64 sin saltos de línea ni parámetros>); "
                                          "no se subiría y quedaría inline")
            else:
                self._inspect_image(v, attrs)
        elif low.startswith("docimg://"):
            self.stats.imagenes += 1
        else:
            self.error("E-SRC", f'src="{v[:60]}": ruta externa; el editor no puede resolverla y el saneador la borra si '
                                "el esquema no es http/https. Ejecuta embed_images.py para embeberla en base64")

    def _inspect_image(self, uri: str, attrs: dict[str, str | None]) -> None:
        self.stats.imagenes += 1
        payload = uri.partition(",")[2]
        size = len(payload) * 3 // 4 - payload.count("=")
        self.stats.bytes_imagenes += size
        props = {p for p, _ in split_declarations(attrs.get("style") or "")}
        has_size = bool(props & {"max-width", "width", "zoom", "height"}) or "width" in attrs
        width = self._image_width(payload) if self.use_pillow else None
        if width is not None:
            if width > IMG_WIDE_PX and not has_size:
                self.warn("A-IMG-ANCHO", f"imagen de {width} px de ancho sin max-width/width: el lector la limita por CSS "
                                         'al 100 %, pero el editor y un anfitrión con CSS propio no; añade style="max-width:100%"')
        elif size > IMG_HEAVY_NO_PIL and not has_size:
            self.warn("A-IMG-ANCHO", f"imagen de {fmt_size(size)} sin max-width/width (no se pudo leer el ancho); "
                                     'añade style="max-width:100%"')
        if size > IMG_HEAVY:
            self.warn("A-IMG-PESO", f"imagen embebida de {fmt_size(size)}: reduce antes con embed_images.py --max-width o comprime")

    @staticmethod
    def _image_width(payload: str) -> int | None:
        try:
            from PIL import Image
            with Image.open(io.BytesIO(base64.b64decode(payload, validate=False))) as im:
                return im.size[0]
        except Exception:
            return None

    def _track_open(self, tag: str, attrs: dict[str, str | None]) -> None:
        if tag in HEADINGS:
            level = int(tag[1])
            if level == 1:
                self.stats.h1 += 1
            elif level == 2:
                self.stats.h2 += 1
            elif level == 3:
                self.stats.h3 += 1
            self._heading = []
            self._heading_level = level
            self._heading_pos = self.getpos()
        elif tag == "table":
            self.table_depth += 1
            self.stats.tablas += 1
        elif tag in CELL_TAGS:
            self.cell_depth += 1
        elif tag == "pre":
            self.pre_depth += 1
            self._pre_frames.append({"has_code": False, "warned": False, "pos": self.getpos()})
        elif tag == "code":
            self.code_depth += 1
            if self._pre_frames:
                self._pre_frames[-1]["has_code"] = True

    # ---- cierre
    def handle_endtag(self, tag: str) -> None:
        if tag in VOID_TAGS:
            return
        idx = next((i for i in range(len(self.stack) - 1, -1, -1) if self.stack[i][0] == tag), None)
        if idx is None:
            if tag == "p" and self._autoclosed_p > 0:
                self._autoclosed_p -= 1
            elif tag not in OPTIONAL_END:
                self.warn("A-CIERRE", f"</{tag}> sin apertura correspondiente")
            return
        popped = self.stack[idx:]
        del self.stack[idx:]
        for opened, line, _col in reversed(popped[1:]):
            if opened not in OPTIONAL_END:
                self.warn("A-CIERRE", f"<{opened}> abierta en línea {line} quedó sin cerrar cuando llegó </{tag}>")
            self._on_close(opened)
        self._on_close(tag)

    def _on_close(self, tag: str) -> None:
        if tag in HEADINGS and self._heading is not None:
            text = normalize_ws(html_lib.unescape("".join(self._heading)))
            level = self._heading_level
            if not text:
                self.empty_headings.append((tag, self._heading_pos[0]))
                if level == 1 and self.first_is_h1 and not self.headings:
                    self.error("E-H1", "el <h1> inicial no tiene texto; es el título del documento", self._heading_pos)
            self.headings.append((level, text, self._heading_pos[0]))
            self._heading = None
        elif tag == "table":
            self.table_depth = max(0, self.table_depth - 1)
        elif tag in CELL_TAGS:
            self.cell_depth = max(0, self.cell_depth - 1)
        elif tag == "pre":
            self.pre_depth = max(0, self.pre_depth - 1)
            frame = self._pre_frames.pop() if self._pre_frames else None
            if frame and not frame["has_code"]:
                self.warn("A-PRE", "<pre> sin <code> dentro: la convención es <pre><code>...</code></pre>", frame["pos"])
        elif tag == "code":
            self.code_depth = max(0, self.code_depth - 1)

    # ---- texto y demás nodos
    def _text(self, fragment: str) -> None:
        if not self.first_tag_seen and fragment.strip():
            self.first_tag_seen = True
            self.error("E-H1", f"hay texto antes del primer elemento ({fragment.strip()[:30]!r}); el fragmento debe empezar por <h1>")
        if self._heading is not None:
            self._heading.append(fragment)

    def handle_data(self, data: str) -> None:
        self._text(data)
        if self.pre_depth == 0 and self.code_depth == 0:
            self.prose_words += len(data.split())
            if ";" in data:
                self.semicolons.extend([self.getpos()[0]] * data.count(";"))
        if self.table_depth > 0 and self.cell_depth == 0 and data.strip():
            top = self.stack[-1][0] if self.stack else ""
            if top in TABLE_PARTS:
                hint = " — un {{ for }} que envuelve filas deja de envolverlas" if "{{" in data else ""
                self.warn("A-TABLA", f"texto {data.strip()[:40]!r} directamente dentro de <{top}> sin celda: el parser lo "
                                     f"expulsa fuera de la tabla (foster parenting){hint}; muévelo a un <td>/<th> o "
                                     "activa «Usa plantillas» si es Scriban")
        if self.pre_depth > 0 and self.code_depth > 0 and ("<" in data or ">" in data) and self._pre_frames:
            frame = self._pre_frames[-1]
            if not frame["warned"]:
                frame["warned"] = True
                self.warn("A-CODE", "código de bloque con < o > sin escapar: usa &lt; y &gt; (un <tag> real se "
                                    "convierte en elemento y el saneador lo borra con lo que abarque)")

    def handle_entityref(self, name: str) -> None:
        self._text(f"&{name};")

    def handle_charref(self, name: str) -> None:
        self._text(f"&#{name};")

    def handle_comment(self, data: str) -> None:
        if not self._comment_noted:
            self._comment_noted = True
            self.add("INFO", "I-COMENTARIO", "hay comentarios HTML: se eliminan al guardar (no cuentes con ellos)")

    def handle_decl(self, decl: str) -> None:
        self.error("E-FRAGMENTO", f"<!{decl[:20]}...>: un fragmento no lleva DOCTYPE")

    def handle_pi(self, data: str) -> None:
        self.error("E-FRAGMENTO", f"<?{data[:20]}...>: instrucción de proceso fuera de lugar en un fragmento")

    def unknown_decl(self, data: str) -> None:
        self.warn("A-CDATA", "sección CDATA/declaración desconocida: se descarta")

    # ---- final
    def finish(self, source_bytes: int) -> None:
        for opened, line, col in reversed(self.stack):
            if opened not in OPTIONAL_END:
                self.warn("A-CIERRE", f"<{opened}> abierta en línea {line} nunca se cierra", (line, col))
            self._on_close(opened)
        self.stack.clear()
        self.stats.bytes_total = source_bytes
        if not self.first_tag_seen:
            self.error("E-H1", "el archivo está vacío o no contiene ningún elemento", (1, 0))
        if self.empty_headings:
            shown = ", ".join(str(line) for _, line in self.empty_headings[:12])
            more = f" y {len(self.empty_headings) - 12} más" if len(self.empty_headings) > 12 else ""
            self.warn("A-VACIO", f"{len(self.empty_headings)} encabezado(s) sin texto legible (líneas {shown}{more}): no "
                                 "entran en el índice «En esta página»; si son separadores <h2><br></h2>, quítalos y "
                                 'da aire con style="margin-top: 1.6em" en el encabezado siguiente',
                      (self.empty_headings[0][1], 0))
        if self.relative_links:
            targets: dict[str, list[int]] = {}
            for href, line in self.relative_links:
                targets.setdefault(href, []).append(line)
            listed = "; ".join(f"{href} (L{', '.join(map(str, lines[:4]))}{'…' if len(lines) > 4 else ''})"
                               for href, lines in targets.items())
            self.add("INFO", "I-ENLACES", f"{len(self.relative_links)} enlace(s) relativo(s) a {len(targets)} destino(s), "
                                          "pendientes de sustituir en KnowledgeHub con clic derecho en el árbol → "
                                          f"Copiar enlace: {listed}", (self.relative_links[0][1], 0))
        if not self.conventions:
            return
        if len(self.semicolons) >= 3 and self.prose_words and len(self.semicolons) * 1000 / self.prose_words > 5:
            rate = len(self.semicolons) * 1000 / self.prose_words
            shown = ", ".join(str(l) for l in sorted(set(self.semicolons))[:12])
            self.warn("A-PUNTOYCOMA", f"{len(self.semicolons)} punto y coma en {self.prose_words} palabras de prosa "
                                      f"({rate:.1f} por mil; en español de México lo normal son 1 o 2): parte la frase en "
                                      f"dos o une con coma y conector (líneas {shown})", (min(self.semicolons), 0))
        if self.stats.h1 != 1:
            self.warn("A-H1-UNICO", f"hay {self.stats.h1} <h1>; debería haber exactamente uno (el título)", (1, 0))
        if self.is_index:
            return          # una portada (kh-module-index) no lleva FAQ ni respeta la jerarquía h2/h3
        h2s = [(text, line) for level, text, line in self.headings if level == 2]
        if not h2s or normalize_title(h2s[-1][0]) != FAQ_TITLE:
            pos = (h2s[-1][1], 0) if h2s else (1, 0)
            self.warn("A-FAQ", "la última <h2> debería ser «Preguntas Frecuentes» (convención del usuario)", pos)
        previous = 0
        for level, text, line in self.headings:
            if previous and level > previous + 1:
                self.warn("A-SALTO-NIVEL", f"<h{level}> «{text[:40]}» salta desde h{previous}; no omitas niveles", (line, 0))
            previous = level


def check_templates(source: str) -> list[Finding]:
    """Comprobaciones sobre el texto crudo: lo que el parser no ve como estructura."""
    found: list[Finding] = []
    for m in TEMPLATE_RE.finditer(source):
        inner = m.group(1)
        if "<" in inner or ">" in inner or "&&" in inner:
            line = source.count("\n", 0, m.start()) + 1
            found.append(Finding("AVISO", line, 0, "A-PLANTILLA",
                                 f"{{{{{normalize_ws(inner)[:40]}}}}} contiene <, > o &&: se escapan al sanear (salvo con "
                                 "«Usa plantillas» activo) y Scriban no tiene lt/gt textuales"))
    opens, closes = source.count("{{"), source.count("}}")
    if opens != closes:
        found.append(Finding("AVISO", 1, 0, "A-PLANTILLA", f"{{{{ y }}}} desbalanceados ({opens} aperturas, {closes} cierres)"))
    return found


# ----------------------------------------------------------------------- por archivo y CLI

def validate_file(path: Path, args) -> tuple[list[Finding], Stats]:
    raw = path.read_bytes()
    try:
        source = raw.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        return [Finding("ERROR", 1, 0, "E-UTF8", f"el archivo no es UTF-8 ({exc}); KnowledgeHub almacena UTF-8 y los "
                                                  "acentos saldrían mal")], Stats(bytes_total=len(raw))
    auditor = FragmentAuditor(allowed_classes={CALLOUT_CLASS, *args.allow_class},
                              class_prefixes=list(args.allow_class_prefix),
                              extra_tags={t.lower() for t in args.allow_tag},
                              conventions=args.convenciones, use_pillow=not args.no_pillow)
    auditor.feed(source)
    auditor.close()
    auditor.finish(len(raw))
    findings = auditor.findings + check_templates(source)
    findings.sort(key=lambda f: (f.linea, f.columna, f.severidad))
    return findings, auditor.stats


def expand_inputs(patterns, recursive: bool) -> list[Path]:
    suffixes = (".html", ".htm")
    found: list[Path] = []
    for raw in patterns:
        p = Path(raw)
        if p.is_dir():
            it = p.rglob("*") if recursive else p.glob("*")
            found.extend(sorted(f for f in it if f.suffix.lower() in suffixes and f.is_file()))
        elif any(ch in raw for ch in "*?["):
            found.extend(Path(x) for x in sorted(glob.glob(raw, recursive=True)))
        else:
            found.append(p)
    unique: list[Path] = []
    seen: set[str] = set()
    for f in found:
        key = str(f.resolve()).lower()
        if key not in seen:
            seen.add(key)
            unique.append(f)
    return unique


def summary_line(stats: Stats) -> str:
    return (f"h1: {stats.h1} · h2: {stats.h2} · h3: {stats.h3} · imágenes: {stats.imagenes} "
            f"({fmt_size(stats.bytes_imagenes)}) · tablas: {stats.tablas} · callouts: {stats.callouts} · "
            f"enlaces: {stats.enlaces} · peso: {fmt_size(stats.bytes_total)}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="validate_html.py",
        description="Comprueba que un fragmento HTML sobrevivirá al saneador de KnowledgeHub y sigue las convenciones.",
        epilog="Salida 0: sin errores · 1: algún ERROR (o AVISO con --strict) · 2: error de uso.")
    parser.add_argument("entradas", nargs="+", help="archivos .html, carpetas o patrones glob")
    parser.add_argument("--recursive", action="store_true", help="con carpetas, busca también en subcarpetas")
    parser.add_argument("--convenciones", action="store_true",
                        help="añade las convenciones del usuario (FAQ final, un solo h1, niveles)")
    parser.add_argument("--allow-class", action="append", default=[], metavar="CLASE",
                        help="clase que el anfitrión declara en AllowedClasses (repetible)")
    parser.add_argument("--allow-class-prefix", action="append", default=[], metavar="PREFIJO",
                        help="prefijo declarado en AllowedClassPrefixes, p. ej. kh- (repetible)")
    parser.add_argument("--allow-tag", action="append", default=[], metavar="ETIQUETA",
                        help="etiqueta que el anfitrión añade a AllowedTags, p. ej. iframe (repetible)")
    parser.add_argument("--strict", action="store_true", help="los AVISO también devuelven 1")
    parser.add_argument("--no-pillow", action="store_true", help="no leer el ancho de las imágenes (solo por peso)")
    parser.add_argument("--quiet", action="store_true", help="solo el veredicto por archivo y el resumen")
    parser.add_argument("--json", action="store_true", help="salida JSON en vez de texto")
    return parser


def main(argv: list[str] | None = None) -> int:
    _configure_stdio()
    args = build_parser().parse_args(argv)
    files = expand_inputs(args.entradas, args.recursive)
    if not files:
        print("[ERROR] Ninguna entrada coincide con lo indicado", file=sys.stderr)
        return 2

    total_errors = total_warnings = 0
    results = []
    for path in files:
        if not path.is_file():
            findings, stats = [Finding("ERROR", 0, 0, "E-ARCHIVO", "no existe")], Stats()
        else:
            findings, stats = validate_file(path, args)
        errors = sum(1 for f in findings if f.severidad == "ERROR")
        warnings = sum(1 for f in findings if f.severidad == "AVISO")
        total_errors += errors
        total_warnings += warnings
        results.append((path, findings, stats, errors, warnings))

    if args.json:
        print(json.dumps([{"archivo": str(p), "errores": e, "avisos": w,
                           "hallazgos": [asdict(f) for f in fs], "resumen": asdict(s)}
                          for p, fs, s, e, w in results], ensure_ascii=False, indent=2))
    else:
        for path, findings, stats, errors, warnings in results:
            print(f"== {path} ==")
            if not args.quiet:
                for f in findings:
                    print(f"  [{f.severidad}] L{f.linea}: {f.codigo}: {f.mensaje}")
                print(f"  [INFO] {summary_line(stats)}")
            verdict = "listo para pegar" if not errors else "corrige los ERROR antes de pegar"
            print(f"  => {errors} error(es), {warnings} aviso(s): {verdict}")
        print(f"Resumen: {len(files)} archivo(s) — {total_errors} error(es), {total_warnings} aviso(s)")

    if total_errors or (args.strict and total_warnings):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
