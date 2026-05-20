using System.Collections.ObjectModel;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class ClientsViewModel : ValidatableViewModelBase
{
    private readonly ClientRepository _repository;
    private readonly IDialogService _dialogService;
    private int _editingId;
    private string _nombre = string.Empty;
    private string _ciNit = string.Empty;
    private string _telefono = string.Empty;
    private string _email = string.Empty;
    private string _direccion = string.Empty;
    private string _buscar = string.Empty;
    private string _estado = string.Empty;
    private bool _activo = true;
    private string _formStatus = "Nuevo cliente";
    private ClientListItem? _selectedClient;

    public ClientsViewModel(ClientRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);
        BuscarCommand = new AsyncRelayCommand(LoadAsync);
        NuevoCommand = new RelayCommand(ResetForm);
    }

    public ObservableCollection<ClientListItem> Clients { get; } = new();
    public AsyncRelayCommand GuardarCommand { get; }
    public AsyncRelayCommand BuscarCommand { get; }
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

    public string CiNit
    {
        get => _ciNit;
        set => SetProperty(ref _ciNit, value);
    }

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
    public string Buscar { get => _buscar; set => SetProperty(ref _buscar, value); }
    public string Estado { get => _estado; set => SetProperty(ref _estado, value); }
    public bool Activo { get => _activo; set => SetProperty(ref _activo, value); }
    public string FormStatus { get => _formStatus; set => SetProperty(ref _formStatus, value); }

    public ClientListItem? SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (SetProperty(ref _selectedClient, value) && value is not null)
            {
                _editingId = value.IdCliente;
                Nombre = value.Nombre;
                CiNit = value.CiNit;
                Telefono = value.Telefono;
                Email = value.Email;
                Direccion = value.Direccion;
                Activo = value.Activo;
                FormStatus = value.Activo
                    ? $"Editando cliente activo ID {_editingId}"
                    : $"Editando cliente inactivo ID {_editingId}";
            }
        }
    }

    public async Task LoadAsync()
    {
        var items = await _repository.GetAllAsync(Buscar.Trim());
        ReplaceCollection(Clients, items);
        Estado = $"{items.Count} cliente(s)";
    }

    private async Task GuardarAsync()
    {
        ValidateNombre();
        ValidateTelefono();
        ValidateEmail();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos del cliente antes de guardar.");
            return;
        }

        await _repository.SaveAsync(new ClientUpsertModel
        {
            IdCliente = _editingId,
            Nombre = Nombre.Trim(),
            CiNit = CiNit.Trim(),
            Telefono = Telefono.Trim(),
            Email = Email.Trim(),
            Direccion = Direccion.Trim(),
            Activo = Activo
        });

        await LoadAsync();
        Estado = Activo
            ? "Cliente guardado como activo."
            : "Cliente guardado como inactivo.";
        ResetForm();
    }

    private void ResetForm()
    {
        _editingId = 0;
        SelectedClient = null;
        Nombre = string.Empty;
        CiNit = string.Empty;
        Telefono = string.Empty;
        Email = string.Empty;
        Direccion = string.Empty;
        Activo = true;
        FormStatus = "Nuevo cliente";
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
