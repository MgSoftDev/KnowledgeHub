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
