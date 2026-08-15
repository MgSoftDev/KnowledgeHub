namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>Everything a renderer needs about WHERE and FOR WHOM a page is being rendered.</summary>
public sealed class TemplateRenderContext
{
    public Guid PagePk { get; set; }
    public string PageTitle { get; set; } = null!;
    public string PageSlug { get; set; } = null!;

    /// <summary>
    /// True while building a PDF. Exposed to the page as <c>kh.is_pdf</c> so a block that only makes
    /// sense on screen — a dashboard of links, a live counter — can be left out of the printed
    /// manual: <c>{{ if !kh.is_pdf }}…{{ end }}</c>.
    /// </summary>
    public bool IsPdfExport { get; set; }

    /// <summary>
    /// Whether the person reading may edit. Decides how much of a failure they are told: an editor
    /// gets the line and the reason, a reader gets a plain notice.
    /// </summary>
    public bool CanEdit { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    /// <summary>The host's permission catalog, offered to pages as <c>kh.roles</c>.</summary>
    public IReadOnlyList<TemplateRoleDto> Roles { get; set; } = Array.Empty<TemplateRoleDto>();

    /// <summary>Data contributed by the host's providers, keyed by their root variable name.</summary>
    public Dictionary<string, object?> Models { get; set; } = new();
}

/// <summary>A role/permission of the host's catalog, as a page sees it.</summary>
public sealed class TemplateRoleDto
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}

/// <summary>What a provider is asked when its data is requested.</summary>
public sealed class TemplateModelContext
{
    public Guid PagePk { get; set; }
    public string PageSlug { get; set; } = null!;
    public bool IsPdfExport { get; set; }
    public string UserName { get; set; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Cancelled when the render budget runs out. Honour it in anything that goes over the network:
    /// a provider that ignores it can hold a page view open past the render timeout.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>A syntax error, with the position already 1-based for humans.</summary>
public sealed class TemplateErrorDto
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = null!;

    public override string ToString() => $"línea {Line}, columna {Column}: {Message}";
}

/// <summary>Self-description of a model, for the editor's explorer.</summary>
public sealed class TemplateModelInfoDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public List<TemplateModelPropertyDto> Properties { get; set; } = new();
}

public sealed class TemplateModelPropertyDto
{
    /// <summary>Name as written in the page, e.g. <c>lineas</c> or <c>lineas[0].ip</c>.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Free text: "texto", "número", "lista de equipos"… It is documentation, not a type check.</summary>
    public string? Type { get; set; }

    public string? Description { get; set; }
}

/// <summary>The whole catalog: what is available and, alongside it, what it currently holds.</summary>
public sealed class TemplateCatalogDto
{
    public List<TemplateModelInfoDto> Models { get; set; } = new();

    /// <summary>
    /// The models rendered as JSON, right now. The declared structure says what SHOULD be there;
    /// this says what IS there, which is what you actually need to write the expression.
    /// </summary>
    public string SampleJson { get; set; } = "{}";
}
