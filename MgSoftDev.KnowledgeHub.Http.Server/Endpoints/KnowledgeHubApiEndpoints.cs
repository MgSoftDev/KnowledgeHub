using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MgSoftDev.KnowledgeHub.Http.Server;

public static class KnowledgeHubApiEndpoints
{
    /// <summary>
    /// Maps the KnowledgeHub service contracts as a minimal API under <paramref name="pattern"/>.
    /// Every endpoint returns HTTP 200 with an ApiResult payload whenever the pipeline worked —
    /// business outcomes (including the publish conflict) travel INSIDE the payload, never as
    /// HTTP errors. Authentication/authorization is the HOST's concern: apply it through
    /// <paramref name="configureGroup"/> (e.g. <c>g =&gt; g.RequireAuthorization()</c>); the
    /// services also guard permissions server-side through IKnowledgeHubUserContext.
    /// </summary>
    public static RouteGroupBuilder MapKnowledgeHubApi(this IEndpointRouteBuilder endpoints,
        string pattern = "/kh/api", Action<RouteGroupBuilder>? configureGroup = null)
    {
        var group = endpoints.MapGroup(pattern);
        configureGroup?.Invoke(group);

        // ---- Identity bootstrap for remote clients -----------------------------------
        group.MapGet("/me", async (IKnowledgeHubUserContext user) =>
        {
            var catalog = await user.GetPermissionCatalogAsync();
            return Results.Ok(new MeResponse
            {
                IsAuthenticated = user.IsAuthenticated,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Permissions = user.Permissions.ToList(),
                Catalog = catalog.OkNotNull ? catalog.Value! : new()
            });
        });

        // ---- Tree & reading ------------------------------------------------------------
        group.MapGet("/pages/tree", async (IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.GetTreeAsync()).ToApi()));

        group.MapGet("/pages/{pagePk:guid}/read", async (Guid pagePk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.GetPageForReadAsync(pagePk)).ToApi()));

        group.MapGet("/pages/{pagePk:guid}/edit", async (Guid pagePk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.GetPageForEditAsync(pagePk)).ToApi()));

        group.MapGet("/versions/{versionPk:guid}", async (Guid versionPk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.GetVersionContentAsync(versionPk)).ToApi()));

        // ---- Versioning & publishing ------------------------------------------------------
        group.MapPost("/pages/draft", async (PageEditDto draft, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.SaveDraftAsync(draft)).ToApi()));

        group.MapPost("/pages/{pagePk:guid}/publish", async (Guid pagePk, PublishPageRequest request, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.PublishAsync(pagePk, request.BaseVersionNumber)).ToApi()));

        group.MapPost("/versions/{versionPk:guid}/restore", async (Guid versionPk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.RestoreVersionAsync(versionPk)).ToApi()));

        group.MapGet("/pages/{pagePk:guid}/versions", async (Guid pagePk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.GetVersionsAsync(pagePk)).ToApi()));

        // ---- Page management -----------------------------------------------------------------
        group.MapGet("/pages/{pagePk:guid}/info", async (Guid pagePk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.GetPageInfoAsync(pagePk)).ToApi()));

        group.MapPost("/pages", async (CreatePageRequest request, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.CreatePageAsync(request.ParentPk, request.Title, request.Slug)).ToApi()));

        group.MapPost("/pages/{pagePk:guid}/rename", async (Guid pagePk, RenamePageRequest request, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.RenamePageAsync(pagePk, request.Title)).ToApi()));

        group.MapPost("/pages/{pagePk:guid}/move", async (Guid pagePk, MovePageRequest request, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.MovePageAsync(pagePk, request.NewParentPk)).ToApi()));

        group.MapPost("/pages/{pagePk:guid}/reorder", async (Guid pagePk, ReorderPageRequest request, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.ReorderAsync(pagePk, request.SortOrder)).ToApi()));

        group.MapPost("/pages/{pagePk:guid}/icon", async (Guid pagePk, SetIconRequest request, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.SetPageIconAsync(pagePk, request.Icon, request.IconColor)).ToApi()));

        group.MapDelete("/pages/{pagePk:guid}", async (Guid pagePk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.DeletePageAsync(pagePk)).ToApi()));

        // ---- Permissions -----------------------------------------------------------------------
        group.MapGet("/pages/{pagePk:guid}/permissions", async (Guid pagePk, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.GetPermissionsAsync(pagePk)).ToApi()));

        group.MapPost("/pages/{pagePk:guid}/permissions", async (Guid pagePk, SetPermissionsRequest request, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.SetPermissionsAsync(pagePk, request.IsPublic, request.Permissions)).ToApi()));

        // ---- Search ---------------------------------------------------------------------------------
        group.MapGet("/search", async (string term, IKnowledgeHubPageService svc) =>
            Results.Ok((await svc.SearchAsync(term)).ToApi()));

        // ---- Images ----------------------------------------------------------------------------------
        group.MapPost("/images", async (UploadImageRequest request, IKnowledgeHubImageService svc) =>
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(request.ContentBase64); }
            catch (FormatException) { return Results.BadRequest("ContentBase64 inválido"); }
            return Results.Ok((await svc.UploadOrReplaceAsync(bytes, request.FileName)).ToApi());
        });

        // ---- HTML rewriter (server resolves display URLs and warms its cache) --------------------------
        group.MapPost("/html/prepare", async (PrepareHtmlRequest request, IKnowledgeHubHtmlImageRewriter rewriter) =>
            Results.Ok((await rewriter.PrepareForDisplayAsync(request.StoredHtml)).ToApi()));

        return group;
    }
}
