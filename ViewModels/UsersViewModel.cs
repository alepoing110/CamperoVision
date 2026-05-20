using System.Collections.ObjectModel;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class UsersViewModel : ValidatableViewModelBase
{
    private readonly UserRepository _repository;
    private readonly IDialogService _dialogService;
    private int _editingId;
    private string _nombre = string.Empty;
    private string _usuario = string.Empty;
    private string _password = string.Empty;
    private string _selectedRole = UserRoles.Vendedor;
    private AppUserListItem? _selectedUser;

    public UsersViewModel(UserRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        Roles = new ObservableCollection<string>(UserRoles.BuilderOptions);
        GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);
        NuevoCommand = new RelayCommand(ResetForm);
    }

    public ObservableCollection<AppUserListItem> Users { get; } = new();
    public ObservableCollection<string> Roles { get; }
    public AsyncRelayCommand GuardarCommand { get; }
    public RelayCommand NuevoCommand { get; }

    public string Nombre
    {
        get => _nombre;
        set
        {
            if (SetProperty(ref _nombre, value))
            {
                ValidateNombre();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Usuario
    {
        get => _usuario;
        set
        {
            if (SetProperty(ref _usuario, value))
            {
                ValidateUsuario();
                GuardarCommand.RaiseCanExecuteChanged();
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
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value))
            {
                ValidateRol();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AppUserListItem? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value is not null)
            {
                _editingId = value.IdUsuario;
                Nombre = value.Nombre;
                Usuario = value.Usuario;
                Password = string.Empty;
                SelectedRole = value.Rol;
            }
        }
    }

    public async Task LoadAsync()
    {
        var users = await _repository.GetAllAsync();
        ReplaceCollection(Users, users);
    }

    private async Task GuardarAsync()
    {
        ValidateNombre();
        ValidateUsuario();
        ValidatePassword();
        ValidateRol();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos del usuario antes de guardar.");
            return;
        }

        await _repository.SaveAsync(new UserUpsertModel
        {
            IdUsuario = _editingId,
            Nombre = Nombre.Trim(),
            Usuario = Usuario.Trim(),
            Rol = SelectedRole,
            Password = Password,
            Activo = true
        });

        await LoadAsync();
        ResetForm();
    }

    private void ResetForm()
    {
        _editingId = 0;
        _selectedUser = null;
        OnPropertyChanged(nameof(SelectedUser));
        Nombre = string.Empty;
        Usuario = string.Empty;
        Password = string.Empty;
        SelectedRole = UserRoles.Vendedor;
        ClearAllErrors();
        GuardarCommand.RaiseCanExecuteChanged();
    }

    private bool CanSave() => !HasErrors && !string.IsNullOrWhiteSpace(Nombre) && !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(SelectedRole);

    private void ValidateNombre()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            SetErrors(nameof(Nombre), new[] { "El nombre es obligatorio." });
            return;
        }

        ClearErrors(nameof(Nombre));
    }

    private void ValidateUsuario()
    {
        if (string.IsNullOrWhiteSpace(Usuario))
        {
            SetErrors(nameof(Usuario), new[] { "El usuario es obligatorio." });
            return;
        }

        if (Usuario.Trim().Length < 3)
        {
            SetErrors(nameof(Usuario), new[] { "El usuario debe tener al menos 3 caracteres." });
            return;
        }

        ClearErrors(nameof(Usuario));
    }

    private void ValidatePassword()
    {
        if (_editingId == 0 && string.IsNullOrWhiteSpace(Password))
        {
            SetErrors(nameof(Password), new[] { "Ingresa una contrasena para el usuario nuevo." });
            return;
        }

        if (!string.IsNullOrWhiteSpace(Password) && Password.Length < 4)
        {
            SetErrors(nameof(Password), new[] { "La contrasena debe tener al menos 4 caracteres." });
            return;
        }

        ClearErrors(nameof(Password));
    }

    private void ValidateRol()
    {
        if (string.IsNullOrWhiteSpace(SelectedRole))
        {
            SetErrors(nameof(SelectedRole), new[] { "Selecciona un rol." });
            return;
        }

        ClearErrors(nameof(SelectedRole));
    }
}
