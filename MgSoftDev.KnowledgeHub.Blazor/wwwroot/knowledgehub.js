// KnowledgeHub JS module. Loaded on demand with a dynamic import from the components that need
// it, so hosts do NOT have to add any <script> tag:
//   import("./_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.js")
// Keep it tiny and dependency-free: it must work the same under WPF (WebView2), Blazor Server
// and WebAssembly.

/**
 * Intrinsic pixel size of an image, which is what the editor cannot tell you otherwise (the DOM
 * width/height only report whatever CSS is applied). Returns null when the image cannot be loaded.
 * @param {string} src Image URL, as it appears in the editor.
 * @returns {Promise<{width: number, height: number} | null>}
 */
export function imageNaturalSize(src) {
    return new Promise(resolve => {
        if (!src) {
            resolve(null);
            return;
        }

        const img = new Image();
        img.onload = () => resolve({ width: img.naturalWidth, height: img.naturalHeight });
        img.onerror = () => resolve(null);
        img.src = src;

        // Already cached by the browser: onload may not fire again in some engines.
        if (img.complete && img.naturalWidth > 0) {
            resolve({ width: img.naturalWidth, height: img.naturalHeight });
        }
    });
}

/**
 * Rendered width of an element, in CSS pixels. Used to record how wide the user dragged the
 * navigation tree: the splitter reports its own number in an undocumented unit, while this is
 * unambiguously px and matches the format of the configured default ("320px").
 * @param {Element} element
 * @returns {number} 0 when the element is gone (e.g. the component was disposed mid-drag).
 */
export function elementWidth(element) {
    return element ? element.getBoundingClientRect().width : 0;
}

/**
 * Saves a file the browser never fetched, streaming it out of .NET.
 *
 * The stream matters: under Blazor Server everything crosses SignalR, and handing the file over as
 * base64 would inflate it by a third and blow the message limit — the same wall the paste path hit.
 * A DotNetStreamReference arrives in chunks instead, so a multi-megabyte PDF gets through.
 * @param {string} fileName Suggested name for the download.
 * @param {string} contentType MIME type, e.g. "application/pdf".
 * @param {any} streamReference A .NET DotNetStreamReference.
 */
export async function downloadFileFromStream(fileName, contentType, streamReference) {
    const buffer = await streamReference.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer], { type: contentType }));

    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName ?? '';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    // Revoking immediately can cancel the download in some engines; one turn later is safe.
    setTimeout(() => URL.revokeObjectURL(url), 0);
}

/**
 * Fits a remembered width to the space actually available now. Without this, a width dragged on a
 * wide monitor comes back on a narrow window (or in a host container that is narrower than the
 * portal) larger than the whole container, and the content column ends up at zero pixels with no
 * obvious way back. Lengths are accepted as px or %, resolved against the container the same way
 * the splitter itself does.
 * @param {Element} container The element the two panes live in.
 * @param {string} stored The remembered width.
 * @param {string} minLength Configured minimum.
 * @param {string} maxLength Configured maximum.
 * @returns {string} A px length, or `stored` untouched when nothing can be measured.
 */
export function fitToContainer(container, stored, minLength, maxLength) {
    if (!container) return stored;

    const total = container.getBoundingClientRect().width;
    if (!(total > 0)) return stored;

    const toPixels = (value, fallback) => {
        if (!value) return fallback;
        const text = String(value).trim().toLowerCase();
        if (text.endsWith('%')) return total * parseFloat(text) / 100;
        if (text.endsWith('px')) return parseFloat(text);
        return fallback;
    };

    const width = toPixels(stored, NaN);
    if (!isFinite(width)) return stored;

    const min = toPixels(minLength, 0);
    const max = Math.min(toPixels(maxLength, total), total);
    return `${Math.round(Math.min(Math.max(width, min), max))}px`;
}

/**
 * localStorage access that can never take the layout down with it. Reading or writing throws
 * outright when storage is disabled (private windows, some WebView2 origins, storage full), and a
 * remembered pane width is not worth an unhandled exception: on failure the module simply stops
 * remembering.
 * @param {string} key
 * @returns {string | null}
 */
export function readSetting(key) {
    try {
        return window.localStorage.getItem(key);
    } catch {
        return null;
    }
}

/**
 * @param {string} key
 * @param {string} value
 * @returns {boolean} Whether it was actually stored.
 */
export function writeSetting(key, value) {
    try {
        window.localStorage.setItem(key, value);
        return true;
    } catch {
        return false;
    }
}

/**
 * Scrolls a page heading into view for the "En esta página" panel.
 *
 * Deliberately does NOT look for the scrolling container: scrollIntoView already walks up every
 * scrollable ancestor and falls back to the window, which keeps this working when the host
 * overrides the module's CSS, nests its own scrollers, or runs inside WebView2. The breathing room
 * above the heading is CSS (scroll-margin-top), not arithmetic here.
 * @param {Element} container
 * @param {string} anchorId
 * @returns {boolean} False when the heading is gone (stale panel), which is not an error.
 */
export function scrollToHeading(container, anchorId) {
    const target = findAnchor(container, anchorId);
    if (!target) return false;

    if (window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches === true) {
        target.scrollIntoView({ block: 'start', behavior: 'auto' });
        return true;
    }

    const scroller = scrollParent(container);
    const before = scroller ? scroller.scrollTop : window.scrollY;
    target.scrollIntoView({ block: 'start', behavior: 'smooth' });

    // Not every environment honours a smooth request: automation browsers and some WebView builds
    // accept it and animate nothing, which would leave the reader exactly where they were with the
    // jump looking broken. Landing on the section matters more than gliding there, so if nothing
    // has moved by the time the animation should have started, snap. A no-op when the target was
    // already at the top, or when the scroller is at its end and cannot go further.
    window.setTimeout(() => {
        const now = scroller ? scroller.scrollTop : window.scrollY;
        if (now === before) target.scrollIntoView({ block: 'start', behavior: 'auto' });
    }, 250);
    return true;
}

/**
 * Marks the panel entry of the heading being read, and keeps marking it while the user scrolls.
 *
 * Stays entirely inside the browser on purpose: reporting each crossed section back to .NET would
 * be one SignalR round trip per section under Blazor Server, on an event that fires continuously
 * while dragging the scrollbar. Toggling a class costs nothing in any of the three hosting models.
 *
 * Idempotent: observing the same container twice replaces the previous observer.
 * @param {Element} container
 */
export function observeHeadings(container) {
    stopObservingHeadings(container);
    if (!container) return;

    const links = new Map();
    for (const link of container.querySelectorAll('[data-kh-anchor]'))
        links.set(link.getAttribute('data-kh-anchor'), link);
    if (links.size === 0) return;

    const headings = [];
    for (const id of links.keys()) {
        const heading = findAnchor(container, id);
        if (heading) headings.push(heading);
    }
    if (headings.length === 0) return;

    const scroller = scrollParent(container);

    // Plain geometry rather than an IntersectionObserver, for two reasons. It always has an answer:
    // an observer watching a band at the top of the viewport marks NOTHING whenever no heading
    // happens to be inside it — a short page, or a section long enough to fill the screen — and the
    // reader is left with a dead panel exactly when they are deepest in the text. And it does not
    // depend on the browser running the intersection step, which some embedded and automated
    // browsers skip entirely.
    const mark = () => {
        const view = scroller ? scroller.getBoundingClientRect() : null;
        const originY = view ? view.top : 0;
        const band = (view ? view.height : window.innerHeight) * 0.3;

        // The section you are reading is the last one whose heading has gone past the band; above
        // the first one, it is the first one.
        let active = headings[0].id;
        for (const heading of headings) {
            if (heading.getBoundingClientRect().top - originY > band) break;
            active = heading.id;
        }

        for (const [id, link] of links) link.classList.toggle('kh-doc-toc-link-active', id === active);
    };

    // Throttled by clock and not by animation frame: a frame callback never arrives in a browser
    // that is not compositing, and then the mark would freeze on whatever it said first.
    let pending = false;
    const onScroll = () => {
        if (pending) return;
        pending = true;
        window.setTimeout(() => { pending = false; mark(); }, 60);
    };

    const source = scroller ?? window;
    source.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onScroll, { passive: true });
    container.__khOutline = { source, onScroll };
    mark();
}

/**
 * @param {Element} container
 */
export function stopObservingHeadings(container) {
    const watch = container?.__khOutline;
    if (!watch) return;

    watch.source.removeEventListener('scroll', watch.onScroll);
    window.removeEventListener('resize', watch.onScroll);
    container.__khOutline = null;
}

/**
 * Attribute selector rather than '#id': a heading titled "1. Introducción" yields an id starting
 * with a digit, which is a valid HTML id but NOT a valid CSS selector. Scoped to the container so
 * an element of the host app carrying the same id cannot win.
 */
function findAnchor(container, anchorId) {
    if (!container || !anchorId) return null;
    try {
        return container.querySelector(`[id="${CSS.escape(anchorId)}"]`);
    } catch {
        return null;
    }
}

/**
 * Nearest scrolling ancestor, found by computed style so it survives the host renaming or
 * restyling anything. Null means the viewport, which IntersectionObserver accepts as the root.
 */
function scrollParent(element) {
    for (let node = element?.parentElement; node; node = node.parentElement) {
        const overflow = window.getComputedStyle(node).overflowY;
        if ((overflow === 'auto' || overflow === 'scroll' || overflow === 'overlay') &&
            node.scrollHeight > node.clientHeight) return node;
    }
    return null;
}
