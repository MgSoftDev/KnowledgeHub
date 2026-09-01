namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Re-indents html so a person can read it in the editor's code view. Optional, like the sanitizer:
/// resolved with <c>GetService</c>, and the toolbar button hides when nothing is registered.
///
/// <para>
/// Deliberately NOT a method on <see cref="IKnowledgeHubHtmlSanitizer"/>. A host is invited to
/// implement its own sanitizer, and a formatting method with a default implementation would leave
/// those hosts with a button that appears and does nothing — the shape of silent failure that cost
/// this repo a release already. Sanitizing is security; formatting is authoring comfort.
/// </para>
/// </summary>
public interface IKnowledgeHubHtmlFormatter
{
    /// <summary>
    /// Same document, laid out to be read. The contract that matters: <b>what the page renders must
    /// not change</b>, so whitespace may only be added where it cannot be seen — around block
    /// boundaries — and never inside a run of inline elements or inside preformatted content.
    /// Running it twice must give the same result as running it once.
    /// </summary>
    string Format(string html);
}
