using System.Collections.ObjectModel;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class ProvidersViewModel : ValidatableViewModelBase
{
    private readonly ProveedorRepository _repository;
    private readonly IDialogService _dialogService;
    private int _editingId;
    private string _nombre = string.Empty;
    private string _nit = string.Empty;
    private string _telefono = string.Empty;
    private string _email = string.Empty;
    private string _direccion = string.Empty;
    private bool _activo = true;
    private ProveedorListItem? _selectedProvider;

    public ProvidersViewModel(ProveedorRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);
        NuevoCommand = new RelayCommand(ResetForm);
    }

    public ObservableCollection<ProveedorListItem> Providers { get; } = new();
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
    public string Nit { get => _nit; set => SetProperty(ref _nit, value); }
    public string Telefono
    {
        get => _telefono;
        set
        {
            if (SetProperty(ref _telefono, value))
            {
                ValidateTelefono();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
            {
                ValidateEmail();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string Direccion { get => _direccion; set => SetProperty(ref _direccion, value); }
    public bool Activo { get => _activo; set => SetProperty(ref _activo, value); }

    public ProveedorListItem? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value) && value is not null)
            {
                _editingId = value.IdProveedor;
                Nombre = value.Nombre;
                Nit = value.Nit;
                Telefono = value.Telefono;
                Email = value.Email;
                Direccion = value.Direccion;
                Activo = value.Activo;
            }
        }
    }

    public async Task LoadAsync()
    {
        var items = await _repository.GetAllAsync();
        ReplaceCollection(Providers, items);
    }

    private async Task GuardarAsync()
    {
        ValidateNombre();
        ValidateTelefono();
        ValidateEmail();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos del proveedor antes de guardar.");
            return;
        }

        var model = new ProveedorUpsertModel
        {
            IdProveedor = _editingId,
            Nombre = Nombre.Trim(),
            Nit = Nit.Trim(),
            Telefono = Telefono.Trim(),
            Email = Email.Trim(),
            Direccion = Direccion.Trim(),
            Activo = Activo
        };

        try
        {
            if (_editingId == 0)
            {
                await _repository.CreateAsync(model);
            }
            else
            {
                await _repository.UpdateAsync(model);
            }

            await LoadAsync();
            ResetForm();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error: {ex.Message}");
        }
    }

    private void ResetForm()
    {
        _editingId = 0;
        _selectedProvider = null;
        OnPropertyChanged(nameof(SelectedProvider));
        Nombre = string.Empty;
        Nit = string.Empty;
        Telefono = string.Empty;
        Email = string.Empty;
        Direccion = string.Empty;
        Activo = true;
        ClearAllErrors();
        GuardarCommand.RaiseCanExecuteChanged();
    }

    private bool CanSave() => !HasErrors && !string.IsNullOrWhiteSpace(Nombre);

    private void ValidateNombre()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            SetErrors(nameof(Nombre), new[] { "El nombre es obligatorio." });
            return;
        }

        ClearErrors(nameof(Nombre));
    }

    private void ValidateTelefono()
    {
        if (!string.IsNullOrWhiteSpace(Telefono) && Telefono.Trim().Length < 6)
        {
            SetErrors(nameof(Telefono), new[] { "El telefono es demasiado corto." });
            return;
        }

        ClearErrors(nameof(Telefono));
    }

    private void ValidateEmail()
    {
        if (!string.IsNullOrWhiteSpace(Email) && !Email.Contains('@'))
        {
            SetErrors(nameof(Email), new[] { "Ingresa un email valido." });
            return;
        }

        ClearErrors(nameof(Email));
    }
}
