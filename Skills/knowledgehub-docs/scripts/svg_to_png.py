r"""
svg_to_png.py — Rasteriza diagramas SVG a PNG para embeberlos en KnowledgeHub.

POR QUÉ EXISTE
  KnowledgeHub no acepta SVG: un <svg> inline lo borra el saneador con todo su contenido, y un
  <img src="data:image/svg+xml;base64,..."> hace que el guardado se RECHACE entero (ImageSharp no
  decodifica SVG). Pero el SVG es la mejor forma de que una IA dibuje un diagrama con precisión.
  Así que la fuente es SVG y lo que entra en el HTML es el PNG que sale de aquí.

CÓMO
  Playwright + Chromium (ya descargado en %LOCALAPPDATA%\ms-playwright) o, si no está, el Edge o
  Chrome instalados (channel msedge / chrome). El SVG se inyecta en una página en blanco con
  set_content y se captura SOLO el elemento <svg> (locator('svg').screenshot), así el PNG mide
  exactamente lo que dice el SVG. device_scale_factor 2 por defecto: el PNG sale al doble de
  píxeles y se ve nítido en pantallas de alta densidad; KnowledgeHub lo reduce a 1600 px de ancho
  al guardar si hace falta.

TAMAÑO
  Se leen width/height del <svg>; si faltan, el viewBox; si no hay nada, 1000x600 con aviso.
  --width fuerza el ancho CSS (la altura se escala en proporción).

LÍMITES
  set_content no tiene URL base: un <image href="foto.png"> dentro del SVG no carga (se avisa).
  Embebe esas imágenes como data URI dentro del SVG, o mejor, no metas imágenes en un diagrama.

CÓDIGOS DE SALIDA
  0 OK · 1 algún SVG no parsea o no se pudo capturar · 2 sin navegador o error de uso.

USO
  python svg_to_png.py diagramas/er-common.svg
  python svg_to_png.py diagramas/ --out build/img --scale 2
  python svg_to_png.py "docs/**/*.svg" --recursive --transparent
"""
from __future__ import annotations

import argparse
import glob
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

INSTALL_HINT = (
    "No hay navegador con el que rasterizar. Instala Playwright y Chromium:\n"
    "  python -m pip install playwright\n"
    "  python -m playwright install chromium\n"
    "o ten Microsoft Edge / Google Chrome instalados (se usan con --browser msedge|chrome)."
)
DEFAULT_WIDTH, DEFAULT_HEIGHT = 1000, 600

SVG_ROOT_RE = re.compile(r"<svg\b(?P<attrs>(?:\"[^\"]*\"|'[^']*'|[^'\">])*)>", re.IGNORECASE | re.DOTALL)
ATTR_RE = re.compile(r"""([^\s"'<>/=]+)\s*=\s*(?:"([^"]*)"|'([^']*)')""")
LENGTH_RE = re.compile(r"^\s*([0-9]*\.?[0-9]+)\s*(px)?\s*$")
EXTERNAL_IMAGE_RE = re.compile(r"<image\b[^>]*\b(?:xlink:)?href\s*=\s*[\"'](?!data:)", re.IGNORECASE)


class SvgError(Exception):
    """El SVG no se puede rasterizar (no parsea, no tiene <svg>, tamaño imposible)."""


class BrowserError(Exception):
    """No hay ningún navegador disponible; no se puede rasterizar nada."""


@dataclass
class RenderResult:
    png: bytes
    css_width: int
    css_height: int
    pixel_width: int
    pixel_height: int
    warnings: list[str] = field(default_factory=list)


def _configure_stdio() -> None:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass


def fmt_size(n: int) -> str:
    return f"{n / 1024:.1f} KB" if n < 1024 * 1024 else f"{n / (1024 * 1024):.2f} MB"


def _root_attributes(svg_text: str) -> tuple[re.Match, dict[str, str]]:
    m = SVG_ROOT_RE.search(svg_text)
    if not m:
        raise SvgError("no contiene un elemento <svg> raíz")
    attrs = {k.lower(): (v1 if v1 is not None else v2 or "") for k, v1, v2 in ATTR_RE.findall(m.group("attrs"))}
    return m, attrs


def svg_size(svg_text: str) -> tuple[int, int, list[str]]:
    """Ancho y alto CSS del SVG, con los avisos de lo que hubo que suponer."""
    _, attrs = _root_attributes(svg_text)
    warnings: list[str] = []

    def length(value: str | None) -> float | None:
        if value is None:
            return None
        m = LENGTH_RE.match(value)
        return float(m.group(1)) if m else None

    width, height = length(attrs.get("width")), length(attrs.get("height"))
    if (width is None or height is None) and attrs.get("viewbox"):
        parts = re.split(r"[\s,]+", attrs["viewbox"].strip())
        if len(parts) == 4:
            try:
                vw, vh = float(parts[2]), float(parts[3])
                if width is None and height is None:
                    width, height = vw, vh
                elif width is None:
                    width = height * vw / vh
                else:
                    height = width * vh / vw
            except (ValueError, ZeroDivisionError):
                pass
    if width is None or height is None or width <= 0 or height <= 0:
        warnings.append(f"sin width/height ni viewBox utilizables; se usa {DEFAULT_WIDTH}x{DEFAULT_HEIGHT}")
        width, height = DEFAULT_WIDTH, DEFAULT_HEIGHT
    if not attrs.get("viewbox"):
        warnings.append("sin viewBox: el diagrama no escalará bien si se cambia el ancho")
    if EXTERNAL_IMAGE_RE.search(svg_text):
        warnings.append("contiene <image href> externo: no cargará (embébelo como data URI dentro del SVG)")
    return int(round(width)), int(round(height)), warnings


def force_width(svg_text: str, width: int) -> str:
    """Reescribe width/height del <svg> raíz a un ancho dado, manteniendo la proporción."""
    m, attrs = _root_attributes(svg_text)
    css_w, css_h, _ = svg_size(svg_text)
    height = max(1, int(round(css_h * width / css_w)))
    new_attrs = re.sub(r"""\s(width|height)\s*=\s*(?:"[^"]*"|'[^']*')""", "", m.group("attrs"), flags=re.IGNORECASE)
    if "viewbox" not in attrs:
        new_attrs += f' viewBox="0 0 {css_w} {css_h}"'
    new_attrs += f' width="{width}" height="{height}"'
    return svg_text[:m.start()] + f"<svg{new_attrs}>" + svg_text[m.end():]


class Rasterizer:
    """Un Chromium por instancia; se reutiliza para todos los SVG de una ejecución."""

    def __init__(self, scale: float = 2.0, transparent: bool = False, browser: str = "auto",
                 timeout_ms: int = 30_000) -> None:
        self.scale = scale
        self.transparent = transparent
        self.browser = browser
        self.timeout_ms = timeout_ms
        self.browser_name: str | None = None
        self._playwright = None
        self._browser = None

    def __enter__(self) -> "Rasterizer":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    def _ensure(self) -> None:
        if self._browser is not None:
            return
        try:
            from playwright.sync_api import sync_playwright
        except ImportError as exc:
            raise BrowserError("Playwright no está instalado.\n" + INSTALL_HINT) from exc

        self._playwright = sync_playwright().start()
        attempts = [("chromium", {}), ("msedge", {"channel": "msedge"}), ("chrome", {"channel": "chrome"})] \
            if self.browser == "auto" else \
            [(self.browser, {} if self.browser == "chromium" else {"channel": self.browser})]
        errors: list[str] = []
        for name, kwargs in attempts:
            try:
                self._browser = self._playwright.chromium.launch(**kwargs)
                self.browser_name = name
                return
            except Exception as exc:  # navegador no instalado: probar el siguiente
                errors.append(f"{name}: {str(exc).splitlines()[0][:120]}")
        self.close()
        raise BrowserError(INSTALL_HINT + "\nIntentos: " + " | ".join(errors))

    def close(self) -> None:
        try:
            if self._browser is not None:
                self._browser.close()
        finally:
            self._browser = None
            if self._playwright is not None:
                self._playwright.stop()
                self._playwright = None

    def render_bytes(self, svg_bytes: bytes, forced_width: int | None = None) -> RenderResult:
        try:
            svg_text = svg_bytes.decode("utf-8-sig")
        except UnicodeDecodeError as exc:
            raise SvgError(f"no es UTF-8: {exc}") from exc
        if forced_width:
            svg_text = force_width(svg_text, forced_width)
        css_w, css_h, warnings = svg_size(svg_text)

        self._ensure()
        background = "transparent" if self.transparent else "#FFFFFF"
        html = ("<!doctype html><html><head><meta charset='utf-8'><style>"
                f"html,body{{margin:0;padding:0;background:{background}}}"
                "svg{display:block}</style></head><body>" + svg_text + "</body></html>")
        page = self._browser.new_page(device_scale_factor=self.scale,
                                      viewport={"width": max(css_w, 1), "height": max(css_h, 1)})
        try:
            page.set_content(html, wait_until="load")
            page.evaluate("document.fonts && document.fonts.ready")
            svg = page.locator("svg").first
            if svg.count() == 0:
                raise SvgError("el navegador no encontró el <svg> tras parsearlo (¿XML mal formado?)")
            png = svg.screenshot(type="png", omit_background=self.transparent, timeout=self.timeout_ms)
        finally:
            page.close()
        return RenderResult(png=png, css_width=css_w, css_height=css_h,
                            pixel_width=int(round(css_w * self.scale)), pixel_height=int(round(css_h * self.scale)),
                            warnings=warnings)

    def render_file(self, path: Path, forced_width: int | None = None) -> RenderResult:
        return self.render_bytes(path.read_bytes(), forced_width)


def expand_inputs(patterns, recursive: bool) -> list[Path]:
    found: list[Path] = []
    for raw in patterns:
        p = Path(raw)
        if p.is_dir():
            it = p.rglob("*.svg") if recursive else p.glob("*.svg")
            found.extend(sorted(f for f in it if f.is_file()))
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


def output_path_for(svg: Path, out: str | None, single: bool) -> Path:
    if out is None:
        return svg.with_suffix(".png")
    target = Path(out)
    if single and target.suffix.lower() == ".png":
        return target
    return target / svg.with_suffix(".png").name


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="svg_to_png.py",
        description="Rasteriza SVG a PNG con Chromium (Playwright) para embeberlos en KnowledgeHub.",
        epilog="Salida 0: OK · 1: algún SVG falló · 2: sin navegador o error de uso.")
    parser.add_argument("entradas", nargs="+", help="archivos .svg, carpetas o patrones glob")
    parser.add_argument("--out", help="archivo .png (una sola entrada) o carpeta de salida")
    parser.add_argument("--scale", type=float, default=2.0, help="device_scale_factor (default 2: nítido en pantallas HiDPI)")
    parser.add_argument("--width", type=int, help="fuerza el ancho CSS del SVG; la altura se escala en proporción")
    parser.add_argument("--transparent", action="store_true", help="fondo transparente en vez de blanco")
    parser.add_argument("--browser", default="auto", choices=["auto", "chromium", "msedge", "chrome"],
                        help="navegador (default auto: chromium de Playwright, luego Edge, luego Chrome)")
    parser.add_argument("--recursive", action="store_true", help="con carpetas, busca también en subcarpetas")
    parser.add_argument("--timeout", type=int, default=30_000, help="milisegundos por SVG (default 30000)")
    return parser


def main(argv: list[str] | None = None) -> int:
    _configure_stdio()
    args = build_parser().parse_args(argv)
    if args.width is not None and args.width <= 0:
        print("[ERROR] --width debe ser un entero positivo", file=sys.stderr)
        return 2
    if args.scale <= 0:
        print("[ERROR] --scale debe ser mayor que 0", file=sys.stderr)
        return 2

    svgs = expand_inputs(args.entradas, args.recursive)
    if not svgs:
        print("[ERROR] Ninguna entrada coincide con lo indicado", file=sys.stderr)
        return 2

    failures = 0
    raster = Rasterizer(scale=args.scale, transparent=args.transparent,
                        browser=args.browser, timeout_ms=args.timeout)
    try:
        with raster:
            for svg in svgs:
                if not svg.is_file():
                    print(f"[ERROR] {svg}: no existe")
                    failures += 1
                    continue
                target = output_path_for(svg, args.out, len(svgs) == 1)
                try:
                    result = raster.render_file(svg, forced_width=args.width)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(result.png)
                except SvgError as exc:
                    print(f"[ERROR] {svg}: {exc}")
                    failures += 1
                    continue
                except BrowserError:
                    raise
                except Exception as exc:          # timeout de Playwright, disco lleno...
                    print(f"[ERROR] {svg}: fallo al capturar: {exc}")
                    failures += 1
                    continue
                for warning in result.warnings:
                    print(f"[AVISO] {svg.name}: {warning}")
                print(f"[OK] {svg} -> {target} ({result.css_width}x{result.css_height} CSS px, "
                      f"{result.pixel_width}x{result.pixel_height} px, {fmt_size(len(result.png))})")
    except BrowserError as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 2

    total = len(svgs)
    browser_note = f" [{raster.browser_name}]" if raster.browser_name else ""
    print(f"Resumen: {total - failures}/{total} convertidos"
          + (f", {failures} con error" if failures else "") + browser_note)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
