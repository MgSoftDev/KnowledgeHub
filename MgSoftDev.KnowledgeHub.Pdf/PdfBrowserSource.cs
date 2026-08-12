namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>Where the Chromium that prints the PDF comes from.</summary>
public enum PdfBrowserSource
{
    /// <summary>
    /// Tries them in order — the installed browser, then a copy bundled with the app, then
    /// Playwright's cache — and keeps the first that works. Default, so the same binary runs on a
    /// laptop, on a server and on a plant machine with no internet.
    /// </summary>
    Auto,

    /// <summary>
    /// The Microsoft Edge (or Chrome) already installed on the machine, through Playwright's
    /// channel mechanism. Downloads NOTHING and is the lightest option on Windows — but note it
    /// only saves the browser: Playwright's own driver still ships with your app (~100 MB).
    /// </summary>
    SystemBrowser,

    /// <summary>
    /// A copy that travels inside the application folder, next to the driver. This is the offline
    /// install: the browser is downloaded on the BUILD machine and goes into the installer, so the
    /// target machine never downloads anything. See the guide for the MSBuild that puts it there.
    /// </summary>
    BundledWithApp,

    /// <summary>
    /// Playwright's per-user cache (<c>%LOCALAPPDATA%\ms-playwright</c>), filled by running
    /// <c>playwright install</c> once on that machine.
    /// </summary>
    PlaywrightCache,

    /// <summary>
    /// Installs the browser into the cache the first time a PDF is exported. Convenient for
    /// developer machines and servers with internet; useless in a plant with no connection, where
    /// it simply reports that it could not download.
    /// </summary>
    DownloadOnDemand
}
