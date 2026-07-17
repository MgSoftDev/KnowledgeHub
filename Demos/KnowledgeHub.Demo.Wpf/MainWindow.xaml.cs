using System.Windows;
using KnowledgeHub.Demo.Wpf.Auth;
using KnowledgeHub.Demo.Wpf.Components;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Storage.LiteDb;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Prism.Mvvm;

namespace KnowledgeHub.Demo.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(DemoUserContext userContext, LiteDbKnowledgeHubOptions storeOptions)
    {
        InitializeComponent();
        DataContext = new MainViewModel(userContext, storeOptions);

        var blazorWebView = new BlazorWebView
        {
            HostPage = "wwwroot/index.html",
            Services = App.Services
        };
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DemoRoot)
        });
        blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;

        RootHost.Children.Clear();
        RootHost.Children.Add(blazorWebView);
    }

    /// <summary>
    /// Maps the virtual host "docs-assets" to the local cache folder so Chromium reads cached
    /// images straight from disk — bypassing Blazor and the IPC channel entirely. Must match
    /// KnowledgeHubOptions.PublicAssetsBaseUrl ("https://docs-assets").
    /// </summary>
    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        using var scope = App.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IKnowledgeHubImageCache>();
        e.WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "docs-assets", cache.CacheFolder, CoreWebView2HostResourceAccessKind.Allow);
    }
}

/// <summary>Chrome around the portal: the status bar identifies user, storage and machine.</summary>
public sealed class MainViewModel : BindableBase
{
    public MainViewModel(DemoUserContext userContext, LiteDbKnowledgeHubOptions storeOptions)
    {
        UserInfo = $"Usuario: {userContext.DisplayName} ({userContext.UserName})";
        RolesInfo = $"Roles: {(userContext.Roles.Count > 0 ? string.Join(", ", userContext.Roles) : "—")}";
        StorageInfo = $"BD: LiteDB · {storeOptions.DatabasePath}";
        MachineInfo = $"Equipo: {Environment.MachineName}";
    }

    public string UserInfo { get; }
    public string RolesInfo { get; }
    public string StorageInfo { get; }
    public string MachineInfo { get; }
}
