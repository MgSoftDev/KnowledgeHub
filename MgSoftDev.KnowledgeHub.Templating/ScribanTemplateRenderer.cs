using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace MgSoftDev.KnowledgeHub.Templating;

/// <summary>
/// The default template engine, on Scriban. Safe as a singleton: it holds only the options and
/// builds everything else per render.
/// </summary>
public sealed class ScribanTemplateRenderer : IKnowledgeHubTemplateRenderer
{
    private readonly KnowledgeHubTemplateOptions _options;

    public ScribanTemplateRenderer(KnowledgeHubTemplateOptions options) => _options = options;

    /// <inheritdoc />
    public ReturningList<TemplateErrorDto> Validate(string html) =>
        ReturningList<TemplateErrorDto>.Try(() =>
        {
            if (string.IsNullOrEmpty(html)) return new List<TemplateErrorDto>();

            // Parse never throws on a syntax error — it collects them — so this checks the template
            // without running a single statement of it.
            var template = Template.Parse(html);
            return template.HasErrors ? Collect(template) : new List<TemplateErrorDto>();
        });

    /// <inheritdoc />
    public async Task<Returning<string>> RenderAsync(string html, TemplateRenderContext context)
    {
        try
        {
            if (string.IsNullOrEmpty(html)) return html;

            var template = Template.Parse(html);
            if (template.HasErrors)
            {
                var first = Collect(template)[0];
                return Returning.Unfinished("La plantilla tiene un error de sintaxis",
                    first.ToString(), UnfinishedInfo.NotifyType.Warning);
            }

            // A FRESH context per render, always: the output budget accumulates and is only cleared
            // by Reset(), so a reused context would silently truncate the second page it rendered.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            var scriban = CreateHardenedContext(cts.Token);
            scriban.PushGlobal(BuildGlobals(context));

            return await template.RenderAsync(scriban);
        }
        catch (ScriptAbortException)
        {
            return Returning.Unfinished("La plantilla tardó demasiado",
                $"Se detuvo tras {_options.TimeoutSeconds} s. Suele ser un bucle sobre demasiados " +
                "datos, o un proveedor de datos que no responde.", UnfinishedInfo.NotifyType.Warning);
        }
        catch (ScriptRuntimeException ex)
        {
            return Returning.Unfinished("La plantilla falló al ejecutarse",
                Describe(ex.Span, ex.OriginalMessage), UnfinishedInfo.NotifyType.Warning);
        }
    }

    /// <summary>
    /// A context with the escape hatches removed and the limits tightened.
    /// <para>
    /// <c>GetDefaultBuiltinObject()</c> hands back a FRESH clone on every call, so removing members
    /// here affects this render only — it is not global state being mutated underneath other users
    /// of Scriban in the same process.
    /// </para>
    /// </summary>
    private TemplateContext CreateHardenedContext(CancellationToken cancellationToken)
    {
        var builtin = TemplateContext.GetDefaultBuiltinObject();

        // include/include_join are already inert without a TemplateLoader — they throw — but a
        // thrown exception is a worse message than a missing function.
        builtin.Remove("include");
        builtin.Remove("include_join");

        // eval compiles and runs a STRING as template code. It does not escape the sandbox, but it
        // makes the stored template stop being an honest description of what the page does, and
        // defeats any future attempt to reason about a template by reading it.
        if (builtin["object"] is ScriptObject objectFunctions)
        {
            objectFunctions.Remove("eval");
            objectFunctions.Remove("eval_template");
        }

        return new TemplateContext(builtin)
        {
            LoopLimit = _options.LoopLimit,
            RecursiveLimit = _options.RecursiveLimit,
            LimitToString = _options.MaxOutputChars,
            CancellationToken = cancellationToken,

            // Reading something nobody defined yields empty instead of throwing. A typo should
            // leave a hole in one line, not replace the page with an error notice.
            // Both flags are needed and they are NOT the same: MemberAccess (on by default) covers
            // `equipos.nocampo`, where the object exists; TargetAccess (off by default) covers
            // `noexiste.nada`, where the variable itself was never defined — which is the typo an
            // editor actually makes, and the one a provider that returned null produces.
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true
        };
    }

    /// <summary>Everything the page can see: the built-in <c>kh</c> plus the host's own models.</summary>
    private static ScriptObject BuildGlobals(TemplateRenderContext context)
    {
        var permissions = context.Permissions;

        var user = new ScriptObject
        {
            ["name"] = context.UserName,
            ["display_name"] = context.DisplayName,
            ["is_authenticated"] = context.IsAuthenticated,
            ["permissions"] = permissions.ToList()
        };
        // {{ if kh.user.has "Role.Produccion" }} — the whole point of showing a block by role.
        user.Import("has", new Func<string, bool>(p =>
            permissions.Contains(p, StringComparer.OrdinalIgnoreCase)));

        var kh = new ScriptObject
        {
            ["user"] = user,
            ["roles"] = context.Roles
                .Select(r => new ScriptObject { ["name"] = r.Name, ["display_name"] = r.DisplayName })
                .ToList(),
            ["page"] = new ScriptObject
            {
                ["pk"] = context.PagePk.ToString(),
                ["title"] = context.PageTitle,
                ["slug"] = context.PageSlug
            },
            ["is_pdf"] = context.IsPdfExport
        };

        var globals = new ScriptObject { ["kh"] = kh };

        // The host's models sit alongside kh, each under its own name. A provider called "kh" would
        // shadow the built-in, so it never gets the chance.
        foreach (var (name, model) in context.Models)
            if (!string.Equals(name, "kh", StringComparison.OrdinalIgnoreCase))
                globals[name] = model;

        return globals;
    }

    private static List<TemplateErrorDto> Collect(Template template) =>
        template.Messages
            .Where(m => m.Type == ParserMessageType.Error)
            .Select(m => new TemplateErrorDto
            {
                // Scriban counts lines and columns from ZERO; humans do not.
                Line = m.Span.Start.Line + 1,
                Column = m.Span.Start.Column + 1,
                Message = m.Message
            })
            .ToList();

    private static string Describe(SourceSpan span, string message) =>
        $"línea {span.Start.Line + 1}, columna {span.Start.Column + 1}: {message}";
}
