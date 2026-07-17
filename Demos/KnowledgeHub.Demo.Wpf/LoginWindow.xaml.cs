using System.Windows;
using KnowledgeHub.Demo.SharedAuth;
using KnowledgeHub.Demo.Wpf.Auth;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.PrismPlus.Returning.Helper.Extension;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Prism.Mvvm;

namespace KnowledgeHub.Demo.Wpf;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(DemoAuthService authService, DemoUserContext userContext)
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(authService, userContext);
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        DataContext = _viewModel;
        Loaded += (_, _) => UserNameBox.Focus();
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Password = PasswordBox.Password;

    private void OnLoginSucceeded() =>
        // The command's async continuation may resume off the UI thread; marshal the
        // window operations back to the dispatcher so WPF objects are touched safely.
        Dispatcher.Invoke(() =>
        {
            DialogResult = true;
            Close();
        });
}

/// <summary>ViewModel for the native login window. Lives with the view, per the Fers WPF style.</summary>
public sealed class LoginViewModel : BindableBase
{
    private readonly DemoAuthService _authService;
    private readonly DemoUserContext _userContext;

    public event Action? LoginSucceeded;

    public LoginViewModel(DemoAuthService authService, DemoUserContext userContext)
    {
        _authService = authService;
        _userContext = userContext;
    }

    public string UserName { get; set => SetProperty(ref field, value); } = string.Empty;
    public string Password { get; set => SetProperty(ref field, value); } = string.Empty;
    public string ErrorMessage { get; set => SetProperty(ref field, value); } = string.Empty;
    public bool Wait { get; set => SetProperty(ref field, value); }

    public string HintText => "Usuarios de prueba: admin · editor1 · prod1 · ofi1 — contraseña Demo123!";

    public AsyncReturningCommand LoginCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            ErrorMessage = string.Empty;

            var result = await _authService.LoginAsync(UserName, Password);
            if (!result.OkNotNull)
                return result;

            _userContext.SetUser(result.Value!);
            return Returning.Success();
        }, () => !Wait && !string.IsNullOrWhiteSpace(UserName))
        .ObservesProperty(() => Wait)
        .ObservesProperty(() => UserName)
        .StartAction(() => Wait = true)
        // EndAction runs on the UI thread, so raising the success event and updating bound
        // properties here (instead of inside the async body) keeps everything thread-safe.
        .EndAction(result =>
        {
            Wait = false;
            if (result.Ok)
                LoginSucceeded?.Invoke();
            else
                ErrorMessage = result.UnfinishedInfo?.Title ?? "No se pudo iniciar sesión. Revisa el registro.";
        });
}
