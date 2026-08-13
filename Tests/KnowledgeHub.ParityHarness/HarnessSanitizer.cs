using MgSoftDev.KnowledgeHub.HtmlSanitizer;

namespace KnowledgeHub.ParityHarness;

/// <summary>
/// The sanitizer configuration, declared ONCE and applied to every container the harness builds.
///
/// That single-declaration bit is the point, not a detail: in http mode the harness runs a client
/// and a server with separate containers, exactly like a WASM host. Pasting is cleaned by one and
/// saving by the other, so two copies of this list that drift apart produce a document that looks
/// right in the editor and loses its markup on the way to the database — with nothing failing.
/// </summary>
internal static class HarnessSanitizer
{
    /// <summary>A class named one by one, the way a host lists a handful of known names.</summary>
    public const string NamedClass = "kh-module-index";

    /// <summary>A family covered by prefix, the way a host handles a set that keeps growing.</summary>
    public const string Prefix = "kh-mi-";

    public static KnowledgeHubSanitizerOptions Options { get; } = new()
    {
        AllowedClasses = { NamedClass },
        AllowedClassPrefixes = { Prefix }
    };
}
