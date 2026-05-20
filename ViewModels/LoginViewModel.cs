using CamperoDesktop.Commands;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class LoginViewModel : ValidatableViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IWindowService _windowService;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = "Ingresa tus credenciales para continuar.";
    private bool _isBusy;

    public LoginViewModel(IAuthenticationService authenticationService, IWindowService windowService)
    {
        _authenticationService = authenticationService;
        _windowService = windowService;
        LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ValidateUsername();
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ValidatePassword();
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand LoginCommand { get; }

    private bool CanLogin() => !IsBusy;

    private async Task LoginAsync()
    {
        ValidateUsername();
        ValidatePassword();

        if (HasErrors)
        {
            StatusMessage = "Corrige los datos marcados antes de continuar.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Validando credenciales...";

            var session = await _authenticationService.LoginAsync(Username.Trim(), Password);
            if (session is null)
            {
                StatusMessage = "Usuario o contrasena incorrectos.";
                return;
            }

            StatusMessage = $"Bienvenido, {session.Nombre}.";
            await _windowService.ShowMainWindowAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo iniciar sesion: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ValidateUsername()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            SetErrors(nameof(Username), new[] { "Ingresa el usuario." });
            return;
        }

        ClearErrors(nameof(Username));
    }

    private void ValidatePassword()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            SetErrors(nameof(Password), new[] { "Ingresa la contrasena." });
            return;
        }

        ClearErrors(nameof(Password));
    }
}
