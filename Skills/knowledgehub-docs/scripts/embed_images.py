r"""
embed_images.py — Embebe en base64 las imágenes de un fragmento HTML para KnowledgeHub.

QUÉ HACE
  Reemplaza cada <img src="ruta/relativa.png|jpg|jpeg|webp|gif|bmp"> por
  <img src="data:image/{mime};base64,..."> . Si el src es un .svg (o ya viene como
  data:image/svg+xml), primero lo rasteriza a PNG con svg_to_png.py (mismo directorio) y
  embebe el PNG.

POR QUÉ
  * El HTML se pega en la vista código del editor: el navegador no puede resolver una ruta
    del disco de quien escribió el documento, y el saneador (Ganss.Xss) descarta cualquier
    src cuyo esquema no sea http, https, data o docimg. Lo que KnowledgeHub SÍ entiende es
    exactamente `data:image/<tipo>;base64,<datos>` (KnowledgeHubHtml.DataUriRegex): al
    guardar intercepta cada una, la decodifica con ImageSharp, la reduce a MaxImageWidth
    (1600 px) de ancho, la recomprime a WebP, la deduplica por SHA256 y la sustituye en el
    HTML almacenado por `docimg://{pk}`.
  * NUNCA se deja un data:image/svg+xml: ImageSharp no decodifica SVG y SaveDraftAsync
    rechaza el guardado entero («No se pudieron procesar N imagen(es) pegada(s)»).
  * El base64 va en una sola línea y sin parámetros extra (;charset, ;name): la regex de
    KnowledgeHub es estricta y lo que no casa se queda inline sin subirse.

--max-width (default 1600)
  Reduce con Pillow (LANCZOS) las imágenes más anchas antes de embeberlas. KnowledgeHub las
  reduciría igual al guardar, pero así el HTML pesa menos —importa en Blazor Server, donde
  el documento viaja entero por SignalR en cada pegado y el límite de fábrica es 32 KB (los
  demos lo suben a 10 MB)— y el editor no sostiene capturas 4K mientras se edita. Los GIF no
  se tocan (pueden ser animados y Pillow perdería los fotogramas): se embeben tal cual.
  BMP se convierte a PNG (mismos píxeles, una fracción del peso).

REGEX EN VEZ DE PARSER
  Un parser reescribiría el documento entero (entidades, cierres implícitos, orden de
  atributos) y aquí solo debe cambiar el valor de src. La regex del <img> consume los
  valores entrecomillados enteros, así que un `>` dentro de alt="a > b" no corta la
  etiqueta, y los atributos se recorren uno a uno para que un alt="src=foo" no se confunda
  con el src.

ERRORES
  Un src que no existe es ERROR: se informan TODOS los perdidos, NO se escribe ningún
  archivo y se sale con 1. Una imagen perdida en silencio es lo peor que le puede pasar a
  una documentación. Las URL http(s) se dejan y se avisan (el validador las marcará como
  ERROR): descárgalas y usa una ruta local.

CÓDIGOS DE SALIDA
  0 OK · 1 algún src perdido/ilegible (nada escrito) · 2 error de uso o de entorno.

USO
  python embed_images.py docs/app/manual.html                        → in-place
  python embed_images.py docs/app/manual.html --out build/manual.html
  python embed_images.py manual.html --max-width 1200 --dry-run
"""
from __future__ import annotations

import argparse
import base64
import html as html_lib
import io
import re
import sys
import urllib.parse
from dataclasses import dataclass, field
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_MAX_WIDTH = 1600            # KnowledgeHubOptions.MaxImageWidth
DEFAULT_SCALE = 2.0                 # device_scale_factor al rasterizar SVG (ver svg_to_png.py)

MIME_BY_EXT = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg",
               ".webp": "image/webp", ".gif": "image/gif", ".bmp": "image/bmp"}
MIME_BY_PIL = {"PNG": "image/png", "JPEG": "image/jpeg", "WEBP": "image/webp",
               "GIF": "image/gif", "BMP": "image/bmp"}
# mime → formato Pillow con el que se reescribe si hay que reescalar (bmp pasa a png).
RESIZABLE = {"image/png": "PNG", "image/jpeg": "JPEG", "image/webp": "WEBP", "image/bmp": "PNG"}

IMG_TAG_RE = re.compile(r"""<img\b(?P<attrs>(?:"[^"]*"|'[^']*'|[^'">])*)>""", re.IGNORECASE)
ATTR_RE = re.compile(r"""([^\s"'<>/=]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'>]+)))?""")
REMOTE_RE = re.compile(r"^(?:[a-z][a-z0-9+.-]*:)?//", re.IGNORECASE)


class EmbedError(Exception):
    """Una imagen concreta no se puede embeber; se acumula y se informa al final."""


class FatalError(Exception):
    """No se puede seguir (sin navegador para SVG, sin svg_to_png.py)."""


@dataclass
class Report:
    embedded: int = 0
    rasterized: int = 0
    resized: int = 0
    already: int = 0
    remote: int = 0
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    lines: list[str] = field(default_factory=list)


def _configure_stdio() -> None:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass


def fmt_size(n: int) -> str:
    return f"{n / 1024:.1f} KB" if n < 1024 * 1024 else f"{n / (1024 * 1024):.2f} MB"


def line_of(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1


def find_src(attrs: str) -> tuple[int, int, str] | None:
    """(inicio, fin, valor) del atributo src dentro de la cadena de atributos del <img>."""
    for m in ATTR_RE.finditer(attrs):
        if m.group(1).lower() == "src":
            value = next((g for g in m.groups()[1:] if g is not None), "")
            return m.start(), m.end(), html_lib.unescape(value)
    return None


def classify(src: str) -> str:
    s = src.strip().lower()
    if s.startswith("data:image/svg+xml"):
        return "data-svg"
    if s.startswith("data:"):
        return "data"
    if s.startswith("docimg://"):
        return "docimg"
    if s.startswith(("http:", "https:")) or REMOTE_RE.match(s):
        return "remote"
    return "local"


def resolve_local(src: str, base_dir: Path) -> Path:
    raw = src.strip()
    if raw.lower().startswith("file:"):
        from urllib.request import url2pathname
        raw = url2pathname(urllib.parse.urlparse(raw).path)
    else:
        raw = urllib.parse.unquote(raw.split("?", 1)[0].split("#", 1)[0])
    p = Path(raw)
    return p if p.is_absolute() else (base_dir / p)


def sniff_mime(data: bytes) -> str | None:
    try:
        from PIL import Image
        with Image.open(io.BytesIO(data)) as im:
            return MIME_BY_PIL.get(im.format or "")
    except Exception:
        return None


def image_width(data: bytes) -> int | None:
    try:
        from PIL import Image
        with Image.open(io.BytesIO(data)) as im:
            return im.size[0]
    except Exception:
        return None


def shrink_if_needed(mime: str, data: bytes, max_width: int, label: str, report: Report) -> tuple[str, bytes]:
    """Reduce a max_width las imágenes reescalables; devuelve (mime, bytes) definitivos."""
    if mime not in RESIZABLE:
        width = image_width(data)
        if width and width > max_width:
            report.warnings.append(f"{label}: GIF de {width} px de ancho; no se reescala aquí "
                                   "(KnowledgeHub lo hará al guardar)")
        return mime, data
    try:
        from PIL import Image, ImageOps
    except ImportError:
        report.warnings.append("Pillow no está instalado: no se reescala nada (pip install pillow)")
        return mime, data

    target_format = RESIZABLE[mime]
    with Image.open(io.BytesIO(data)) as opened:
        im = ImageOps.exif_transpose(opened) or opened
        width, height = im.size
        needs_resize = width > max_width
        if not needs_resize and mime != "image/bmp":
            return mime, data
        if needs_resize:
            new_height = max(1, round(height * max_width / width))
            im = im.resize((max_width, new_height), Image.Resampling.LANCZOS)
            report.resized += 1
            report.lines.append(f"[INFO] {label}: {width}x{height} → {max_width}x{new_height} px")

        buffer = io.BytesIO()
        if target_format == "JPEG":
            if im.mode not in ("RGB", "L"):
                im = im.convert("RGB")
            im.save(buffer, format="JPEG", quality=88, optimize=True)
            return "image/jpeg", buffer.getvalue()
        if target_format == "WEBP":
            im.save(buffer, format="WEBP", quality=88, method=6)
            return "image/webp", buffer.getvalue()
        im.save(buffer, format="PNG", optimize=True)
        return "image/png", buffer.getvalue()


class LazyRasterizer:
    """Arranca Chromium solo si aparece un SVG; un único navegador para todo el documento."""

    def __init__(self, scale: float) -> None:
        self.scale = scale
        self._module = None
        self._raster = None

    def render(self, svg_bytes: bytes, label: str, report: Report) -> bytes:
        if self._raster is None:
            if str(SCRIPT_DIR) not in sys.path:
                sys.path.insert(0, str(SCRIPT_DIR))
            try:
                import svg_to_png
            except ImportError as exc:
                raise FatalError(f"no se encuentra svg_to_png.py junto a este script ({SCRIPT_DIR})") from exc
            self._module = svg_to_png
            self._raster = svg_to_png.Rasterizer(scale=self.scale)
        try:
            result = self._raster.render_bytes(svg_bytes)
        except self._module.SvgError as exc:
            raise EmbedError(f"{label}: SVG no rasterizable: {exc}") from exc
        except self._module.BrowserError as exc:
            raise FatalError(f"sin navegador para rasterizar SVG:\n{exc}") from exc
        for warning in result.warnings:
            report.warnings.append(f"{label}: {warning}")
        return result.png

    def close(self) -> None:
        if self._raster is not None:
            self._raster.close()
            self._raster = None


def decode_svg_data_uri(src: str) -> bytes:
    header, _, payload = src.partition(",")
    if not payload:
        raise EmbedError("data:image/svg+xml sin datos")
    if ";base64" in header.lower():
        return base64.b64decode(payload, validate=False)
    return urllib.parse.unquote(payload).encode("utf-8")


def embed_document(html: str, base_dir: Path, max_width: int, scale: float, report: Report) -> str:
    lazy = LazyRasterizer(scale)
    pieces: list[str] = []
    last = 0
    try:
        for m in IMG_TAG_RE.finditer(html):
            attrs = m.group("attrs")
            line = line_of(html, m.start())
            found = find_src(attrs)
            if found is None:
                report.warnings.append(f"línea {line}: <img> sin src; se deja tal cual")
                continue
            a_start, a_end, value = found
            kind = classify(value)
            label = f"línea {line}: {value[:60]}{'...' if len(value) > 60 else ''}"

            if kind in ("data", "docimg"):
                report.already += 1
                continue
            if kind == "remote":
                report.remote += 1
                report.warnings.append(f"{label}: URL remota; no se embebe (descárgala y usa una ruta local)")
                continue

            try:
                if kind == "data-svg":
                    png = lazy.render(decode_svg_data_uri(value), label, report)
                    mime, data = shrink_if_needed("image/png", png, max_width, label, report)
                    report.rasterized += 1
                else:
                    path = resolve_local(value, base_dir)
                    if not path.is_file():
                        raise EmbedError(f"{label}: no existe {path}")
                    if path.suffix.lower() == ".svg":
                        png = lazy.render(path.read_bytes(), label, report)
                        mime, data = shrink_if_needed("image/png", png, max_width, label, report)
                        report.rasterized += 1
                    else:
                        raw = path.read_bytes()
                        mime = MIME_BY_EXT.get(path.suffix.lower()) or sniff_mime(raw)
                        if mime is None:
                            raise EmbedError(f"{label}: extensión desconocida y Pillow no reconoce el formato")
                        mime, data = shrink_if_needed(mime, raw, max_width, label, report)
            except EmbedError as exc:
                report.errors.append(str(exc))
                continue

            uri = f"data:{mime};base64,{base64.b64encode(data).decode('ascii')}"
            new_attrs = attrs[:a_start] + f'src="{uri}"' + attrs[a_end:]
            pieces.append(html[last:m.start()])
            pieces.append(f"<img{new_attrs}>")
            last = m.end()
            report.embedded += 1
            report.lines.append(f"[OK] {label} -> {mime}, {fmt_size(len(data))}")
    finally:
        lazy.close()
    pieces.append(html[last:])
    return "".join(pieces)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="embed_images.py",
        description="Embebe en base64 las imágenes de un fragmento HTML (rasterizando los SVG) para KnowledgeHub.",
        epilog="Salida 0: OK · 1: algún src perdido (no se escribe nada) · 2: error de uso o de entorno.")
    parser.add_argument("html", help="fragmento .html de entrada")
    parser.add_argument("--out", help="archivo de salida (por defecto se sobrescribe la entrada)")
    parser.add_argument("--base", help="carpeta base para las rutas relativas (por defecto, la del HTML)")
    parser.add_argument("--max-width", type=int, default=DEFAULT_MAX_WIDTH,
                        help=f"ancho máximo en px; por encima se reescala con Pillow (default {DEFAULT_MAX_WIDTH})")
    parser.add_argument("--scale", type=float, default=DEFAULT_SCALE,
                        help="device_scale_factor para rasterizar SVG (ver svg_to_png.py)")
    parser.add_argument("--warn-size-mb", type=float, default=8.0,
                        help="avisa si el HTML final supera este tamaño (default 8)")
    parser.add_argument("--dry-run", action="store_true", help="solo informa; no escribe nada")
    return parser


def main(argv: list[str] | None = None) -> int:
    _configure_stdio()
    args = build_parser().parse_args(argv)
    if args.max_width <= 0:
        print("[ERROR] --max-width debe ser positivo", file=sys.stderr)
        return 2

    src_path = Path(args.html)
    if not src_path.is_file():
        print(f"[ERROR] no existe {src_path}", file=sys.stderr)
        return 2
    base_dir = Path(args.base) if args.base else src_path.parent
    try:
        with open(src_path, encoding="utf-8-sig", newline="") as fh:   # newline="" conserva CRLF
            html = fh.read()
    except UnicodeDecodeError as exc:
        print(f"[ERROR] {src_path} no es UTF-8 ({exc}); KnowledgeHub almacena UTF-8", file=sys.stderr)
        return 2

    report = Report()
    try:
        result = embed_document(html, base_dir, args.max_width, args.scale, report)
    except FatalError as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 2

    for line in report.lines:
        print(line)
    for warning in report.warnings:
        print(f"[AVISO] {warning}")
    if report.errors:
        for error in report.errors:
            print(f"[ERROR] {error}")
        print(f"[ERROR] {len(report.errors)} imagen(es) sin resolver: NO se ha escrito nada. "
              "Corrige las rutas y vuelve a ejecutar.")
        return 1

    target = Path(args.out) if args.out else src_path
    final_bytes = len(result.encode("utf-8"))
    if args.dry_run:
        print(f"[INFO] --dry-run: no se escribe {target}")
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        with open(target, "w", encoding="utf-8", newline="") as fh:
            fh.write(result)

    print(f"Resumen: {report.embedded} embebida(s) ({report.rasterized} SVG rasterizado(s), "
          f"{report.resized} reescalada(s)), {report.already} ya embebida(s)/docimg, "
          f"{report.remote} remota(s) sin tocar · HTML final: {fmt_size(final_bytes)} → {target}")
    if report.embedded == 0 and report.already == 0:
        print("[INFO] no había imágenes que embeber")
    if final_bytes > args.warn_size_mb * 1024 * 1024:
        print(f"[AVISO] el HTML pesa más de {args.warn_size_mb:g} MB: en Blazor Server el pegado viaja por "
              "SignalR y con MaximumReceiveMessageSize bajo se pierde SIN error; baja --max-width o divide la página")
    return 0


if __name__ == "__main__":
    sys.exit(main())
