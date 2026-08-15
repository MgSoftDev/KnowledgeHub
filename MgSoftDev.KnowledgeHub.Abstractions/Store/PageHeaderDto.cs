namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>
/// Lightweight page header used by the read path.
///
/// <para>
/// <c>Slug</c> and <c>UsesTemplates</c> travel HERE, in the header, because the read path already
/// loads it: deciding later whether a page needs its placeholders filled would cost a second query
/// on every single page view. <c>Slug</c> is offered to templates as <c>kh.page.slug</c>.
/// </para>
/// </summary>
public sealed record PageHeaderDto(Guid Pk, string Title, Guid? PublishedVersionPk,
    string? Icon = null, string? IconColor = null,
    string? Slug = null, bool UsesTemplates = false);
