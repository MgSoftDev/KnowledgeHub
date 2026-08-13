using MgSoftDev.KnowledgeHub.HtmlSanitizer;

namespace KnowledgeHub.Demo.Wasm;

/// <summary>
/// The sanitizer rules of this host, declared ONCE and applied by both <c>Program.cs</c> — the
/// client's and the API server's. This project is referenced by the server, which is what makes
/// sharing them possible.
///
/// Doing it this way is the whole point. WASM has TWO containers: the editor cleans what you paste
/// in the browser, and the API server cleans again right before storing. Configure only the client
/// and everything looks perfect on screen while the server, still on the factory rules, strips the
/// markup on its way to the database. It fails on save, far from where you changed anything.
/// </summary>
public static class DemoSanitizer
{
    public static KnowledgeHubSanitizerOptions Options { get; } = new()
    {
        // A layout of this host's own, styled by its stylesheet. Without declaring it, saving would
        // quietly drop the classes and the block would lose its shape.
        AllowedClasses = { "demo-ficha" },

        // Prefix instead of names for a family that grows: every demo-ico--* added later works
        // without touching this file. A class nobody remembered to register is indistinguishable
        // from pasted junk, and junk is exactly what the sanitizer is for.
        AllowedClassPrefixes = { "demo-ficha-", "demo-ico--" }
    };
}
