using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// Signed-in user the components see. Admin by default, because most screens hide everything
/// otherwise and a test that renders an empty "Sin permiso" box proves nothing.
/// </summary>
public sealed class FakeUser(bool admin = true) : IKnowledgeHubUserContext
{
    public bool IsAuthenticated => true;
    public string UserName => "test";
    public string DisplayName => "Usuario de prueba";

    public IReadOnlyList<string> Permissions => admin
        ? [KnowledgeHubPermissions.Admin, KnowledgeHubPermissions.Templates]
        : [];

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        Task.FromResult<ReturningList<PermissionInfo>>(new List<PermissionInfo>
        {
            new("Role.Admin", "Administrador"),
            new("Role.Produccion", "Producción")
        });
}

/// <summary>
/// Page service with a fixed two-page tree, enough for every component to render something real.
///
/// It fakes the SERVICE, not the store: these tests are about components, and whether the core
/// fills a DTO correctly is what the parity harness checks — that split is exactly why the checkbox
/// bug of v0.19.1 was invisible here and caught there.
/// </summary>
public sealed class FakePageService : IKnowledgeHubPageService
{
    public static readonly Guid RootPk = Guid.Parse("019f8d8a-0001-7000-8000-000000000001");
    public static readonly Guid ChildPk = Guid.Parse("019f8d8a-0002-7000-8000-000000000002");
    public static readonly Guid VersionPk = Guid.Parse("019f8d8a-0003-7000-8000-000000000003");

    /// <summary>Counts tree loads, so a test can prove a refresh actually happened.</summary>
    public int TreeLoads { get; private set; }

    /// <summary>Body the reader gets. Settable so a test can hand it headings to index.</summary>
    public string ContentHtml { get; set; } = "<p>Contenido de prueba</p>";

    public Task<ReturningList<PageTreeNodeDto>> GetTreeAsync()
    {
        TreeLoads++;
        // NEW instances on every call, like the real service: that is what makes Radzen rebuild its
        // items and re-raise the selection event — the echo behind the v0.19.3 bug.
        return Task.FromResult<ReturningList<PageTreeNodeDto>>(new List<PageTreeNodeDto>
        {
            new()
            {
                Pk = RootPk, Title = "Manual", Slug = "manual", IsPublic = true,
                HasPublishedVersion = true,
                Children =
                [
                    new PageTreeNodeDto
                    {
                        Pk = ChildPk, Fk_DocPageParent = RootPk, Title = "Primeros pasos",
                        Slug = "primeros-pasos", IsPublic = true, HasPublishedVersion = true
                    }
                ]
            }
        });
    }

    public Task<Returning<PageReadDto>> GetPageForReadAsync(Guid pagePk) =>
        Task.FromResult<Returning<PageReadDto>>(new PageReadDto
        {
            PagePk = pagePk, VersionPk = VersionPk, Title = "Manual",
            ContentHtml = ContentHtml, VersionNumber = 1,
            Status = DocPageStatus.Published, PublishedAt = DateTime.Now
        });

    public Task<Returning<PageReadDto>> GetPageForExportAsync(Guid pagePk) => GetPageForReadAsync(pagePk);

    public Task<Returning<PageReadDto>> GetVersionContentAsync(Guid versionPk) => GetPageForReadAsync(RootPk);

    public Task<Returning<PageEditDto>> GetPageForEditAsync(Guid pagePk) =>
        Task.FromResult<Returning<PageEditDto>>(new PageEditDto
        {
            PagePk = pagePk, Slug = "manual", Title = "Manual",
            ContentHtml = "<p>Contenido de prueba</p>", BaseVersionNumber = 1
        });

    public Task<Returning<PageInfoDto>> GetPageInfoAsync(Guid pagePk) =>
        Task.FromResult<Returning<PageInfoDto>>(new PageInfoDto
        {
            Pk = pagePk, Title = "Manual", Slug = "manual", SortOrder = 1
        });

    public Task<ReturningList<VersionListItemDto>> GetVersionsAsync(Guid pagePk) =>
        Task.FromResult<ReturningList<VersionListItemDto>>(new List<VersionListItemDto>
        {
            new() { Pk = VersionPk, VersionNumber = 1, Status = DocPageStatus.Published,
                    IsCurrentPublished = true, RowCreateDate = DateTime.Now, AuthorName = "test" }
        });

    public Task<Returning<PagePermissionsDto>> GetPermissionsAsync(Guid pagePk) =>
        Task.FromResult<Returning<PagePermissionsDto>>(new PagePermissionsDto { IsPublic = true });

    public Task<ReturningList<SearchResultDto>> SearchAsync(string term) =>
        Task.FromResult<ReturningList<SearchResultDto>>(new List<SearchResultDto>
        {
            new() { PagePk = RootPk, Title = "Manual", Slug = "manual", Snippet = "…resultado…" }
        });

    public Task<ReturningList<TemplateErrorDto>> ValidateTemplateAsync(string html) =>
        Task.FromResult<ReturningList<TemplateErrorDto>>(new List<TemplateErrorDto>());

    public Task<Returning<TemplateCatalogDto>> GetTemplateCatalogAsync() =>
        Task.FromResult<Returning<TemplateCatalogDto>>(new TemplateCatalogDto());

    // ---- escrituras: aceptan y no hacen nada; los componentes solo miran si fue Ok ----

    public Task<Returning<Guid>> CreatePageAsync(Guid? parentPk, string title, string? slug = null) =>
        Task.FromResult<Returning<Guid>>(ChildPk);

    public Task<Returning<int>> SaveDraftAsync(PageEditDto draft) => Task.FromResult<Returning<int>>(2);

    public Task<Returning> PublishAsync(Guid pagePk, int baseVersionNumber) => Ok();
    public Task<Returning> RestoreVersionAsync(Guid versionPk) => Ok();
    public Task<Returning> RenamePageAsync(Guid pagePk, string title) => Ok();
    public Task<Returning> MovePageAsync(Guid pagePk, Guid? newParentPk) => Ok();
    public Task<Returning> ReorderAsync(Guid pagePk, int sortOrder) => Ok();
    public Task<Returning> MovePageOrderAsync(Guid pagePk, PageMoveDirection direction) => Ok();
    public Task<Returning<int>> NormalizeAllPageOrdersAsync() => Task.FromResult<Returning<int>>(0);
    public Task<Returning> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor) => Ok();
    public Task<Returning> SetPageExcludeFromPdfAsync(Guid pagePk, bool excludeFromPdf) => Ok();
    public Task<Returning> SetPageUsesTemplatesAsync(Guid pagePk, bool usesTemplates) => Ok();
    public Task<Returning> DeletePageAsync(Guid pagePk) => Ok();

    public Task<Returning> SetPermissionsAsync(Guid pagePk, bool isPublic, IReadOnlyList<string> permissions) => Ok();

    private static Task<Returning> Ok() => Task.FromResult(Returning.Success());
}

/// <summary>Image rewriter that returns the html untouched: the components only care that it works.</summary>
public sealed class FakeImageRewriter : IKnowledgeHubHtmlImageRewriter
{
    public Task<Returning<HtmlRewriteResult>> PrepareForDisplayAsync(string storedHtml) =>
        Task.FromResult<Returning<HtmlRewriteResult>>(new HtmlRewriteResult { Html = storedHtml });
}

/// <summary>Only the diagnostics panel needs this one, to offer its orphan-image cleanup.</summary>
public sealed class FakeImageService : IKnowledgeHubImageService
{
    public Task<Returning<Guid>> UploadOrReplaceAsync(byte[] originalBytes, string fileName) =>
        Task.FromResult<Returning<Guid>>(Guid.Empty);

    public Task<Returning<OrphanImageReportDto>> AnalyzeOrphanImagesAsync() =>
        Task.FromResult<Returning<OrphanImageReportDto>>(new OrphanImageReportDto());

    public Task<Returning<int>> DeleteOrphanImagesAsync() => Task.FromResult<Returning<int>>(0);
}

/// <summary>Swallows the metrics the reader records on every navigation.</summary>
public sealed class FakeDiagnostics : IKnowledgeHubDiagnostics
{
    public DiagnosticsSnapshot? Last { get; private set; }
    public long CumulativeHits => 0;
    public long CumulativeMisses => 0;

    public void Record(DiagnosticsSnapshot snapshot) => Last = snapshot;

    public void ResetCumulative() { }
}
