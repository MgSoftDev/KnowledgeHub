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
