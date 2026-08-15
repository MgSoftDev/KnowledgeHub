using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;

namespace KnowledgeHub.Demo.BlazorServer.Templates;

/// <summary>
/// Example of what a HOST contributes to page templates: the plant's equipment, so the IT page that
/// lists lines, PCs, IPs and PLCs stops being a copy that somebody has to remember to update.
///
/// In a real application this would query a table or an internal API. Two things about doing that
/// are worth copying from here:
///
/// <list type="bullet">
/// <item>
/// <b>Project to a small shape.</b> Handing over an EF entity would let a page walk its navigation
/// properties, and returning something like a DbContext would expose far more than intended. What
/// leaves this method is a flat record with exactly the fields the documentation shows.
/// </item>
/// <item>
/// <b>Honour the cancellation token</b> in anything that goes over the network: it carries the
/// render timeout, and a provider that ignores it holds the page view open past it.
/// </item>
/// </list>
///
/// It is asked on EVERY view of a page that uses it — that is the point, the data has to be current
/// — so if the source is expensive, cache HERE, where the cost is known.
/// </summary>
public sealed class EquiposTemplateModelProvider : IKnowledgeHubTemplateModelProvider
{
    /// <summary>Root variable: <c>{{ for e in equipos }}</c>.</summary>
    public string Name => "equipos";

    public TemplateModelInfoDto Describe() => new()
    {
        Name = Name,
        Description = "Equipos de planta: línea, estación, PC, IP y PLC.",
        Properties =
        [
            new TemplateModelPropertyDto { Name = "[].linea", Type = "texto", Description = "Línea de producción" },
            new TemplateModelPropertyDto { Name = "[].estacion", Type = "texto", Description = "Estación dentro de la línea" },
            new TemplateModelPropertyDto { Name = "[].pc", Type = "texto", Description = "Nombre del equipo" },
            new TemplateModelPropertyDto { Name = "[].ip", Type = "texto", Description = "Dirección IP" },
            new TemplateModelPropertyDto { Name = "[].plc", Type = "texto", Description = "Modelo del PLC" }
        ]
    };

    public Task<object?> GetModelAsync(TemplateModelContext context) =>
        Task.FromResult<object?>(new[]
        {
            new { linea = "Línea 1", estacion = "Ensamble", pc = "PC-L1-ENS", ip = "10.20.1.11", plc = "S7-1500" },
            new { linea = "Línea 1", estacion = "Prueba", pc = "PC-L1-PRU", ip = "10.20.1.12", plc = "S7-1200" },
            new { linea = "Línea 2", estacion = "Ensamble", pc = "PC-L2-ENS", ip = "10.20.2.11", plc = "S7-1500" },
            new { linea = "Línea 2", estacion = "Empaque", pc = "PC-L2-EMP", ip = "10.20.2.13", plc = "CompactLogix" }
        });
}
