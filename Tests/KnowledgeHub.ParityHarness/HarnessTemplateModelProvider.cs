using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;

namespace KnowledgeHub.ParityHarness;

/// <summary>
/// Stand-in for a host's data source. Two of its three fields exist to be checked:
/// <c>Peligroso</c> carries markup, so the run can prove the RENDERED output is sanitized (Scriban
/// escapes nothing), and <c>Fallar</c> makes it throw on demand, to prove one broken provider costs
/// its own variable and not the page.
/// </summary>
public sealed class HarnessTemplateModelProvider : IKnowledgeHubTemplateModelProvider
{
    /// <summary>Flipped by the script to exercise the failure path.</summary>
    public static bool Fallar { get; set; }

    public string Name => "equipos";

    public TemplateModelInfoDto Describe() => new()
    {
        Name = Name,
        Description = "Equipos de prueba",
        Properties =
        [
            new TemplateModelPropertyDto { Name = "[].nombre", Type = "texto" },
            new TemplateModelPropertyDto { Name = "[].ip", Type = "texto" }
        ]
    };

    public Task<object?> GetModelAsync(TemplateModelContext context)
    {
        if (Fallar) throw new InvalidOperationException("proveedor caído a propósito");

        return Task.FromResult<object?>(new[]
        {
            new { nombre = "PC-1", ip = "10.0.0.1", peligroso = "<script>alert(1)</script>" },
            new { nombre = "PC-2", ip = "10.0.0.2", peligroso = "sin nada" }
        });
    }
}
