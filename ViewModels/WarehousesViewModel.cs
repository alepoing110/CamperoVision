using System.Collections.ObjectModel;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class WarehousesViewModel : ValidatableViewModelBase
{
    private readonly WarehouseRepository _repository;
    private readonly IDialogService _dialogService;
    private int _editingId;
    private string _nombre = string.Empty;
    private string _direccion = string.Empty;
    private string _responsable = string.Empty;
    private string _buscar = string.Empty;
    private string _estado = string.Empty;
    private bool _activo = true;
    private WarehouseListItem? _selectedWarehouse;

    public WarehousesViewModel(WarehouseRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);
        BuscarCommand = new AsyncRelayCommand(LoadAsync);
        NuevoCommand = new RelayCommand(ResetForm);
        DesactivarCommand = new AsyncRelayCommand(DesactivarAsync, () => SelectedWarehouse is not null && SelectedWarehouse.Activo);
    }

    public ObservableCollection<WarehouseListItem> Warehouses { get; } = new();
    public AsyncRelayCommand GuardarCommand { get; }
    public AsyncRelayCommand BuscarCommand { get; }
    public AsyncRelayCommand DesactivarCommand { get; }
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

    public string Direccion { get => _direccion; set => SetProperty(ref _direccion, value); }

    public string Responsable
    {
        get => _responsable;
        set
        {
            if (SetProperty(ref _responsable, value))
            {
                ValidateResponsable();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    public string Buscar { get => _buscar; set => SetProperty(ref _buscar, value); }
    public string Estado { get => _estado; set => SetProperty(ref _estado, value); }

    public WarehouseListItem? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value) && value is not null)
            {
                _editingId = value.IdAlmacen;
                Nombre = value.Nombre;
                Direccion = value.Direccion;
                Responsable = value.Responsable;
                Activo = value.Activo;
                DesactivarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync()
    {
        List<WarehouseListItem> items = await _repository.GetAllAsync(Buscar.Trim());
        ReplaceCollection(Warehouses, items);
        Estado = $"{items.Count} almacen(es)";
        DesactivarCommand.RaiseCanExecuteChanged();
    }

    private async Task GuardarAsync()
    {
        ValidateNombre();
        ValidateResponsable();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos del almacen antes de guardar.");
            return;
        }

        await _repository.SaveAsync(new WarehouseUpsertModel
        {
            IdAlmacen = _editingId,
            Nombre = Nombre.Trim(),
            Direccion = Direccion.Trim(),
            Responsable = Responsable.Trim(),
            Activo = Activo
        });

        await LoadAsync();
        ResetForm();
    }

    private async Task DesactivarAsync()
    {
        if (SelectedWarehouse is null)
        {
            _dialogService.ShowWarning("Selecciona un almacen para desactivar.");
            return;
        }

        await _repository.DeactivateAsync(SelectedWarehouse.IdAlmacen);
        _dialogService.ShowInfo($"Almacen {SelectedWarehouse.Nombre} desactivado correctamente.");
        await LoadAsync();
        ResetForm();
    }

    private void ResetForm()
    {
        _editingId = 0;
        _selectedWarehouse = null;
        OnPropertyChanged(nameof(SelectedWarehouse));
        Nombre = string.Empty;
        Direccion = string.Empty;
        Responsable = string.Empty;
        Activo = true;
        ClearAllErrors();
        GuardarCommand.RaiseCanExecuteChanged();
        DesactivarCommand.RaiseCanExecuteChanged();
    }

    private bool CanSave() => !HasErrors && !string.IsNullOrWhiteSpace(Nombre);

    private void ValidateNombre()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            SetErrors(nameof(Nombre), new[] { "El nombre del almacen es obligatorio." });
            return;
        }

        ClearErrors(nameof(Nombre));
    }

    private void ValidateResponsable()
    {
        if (!string.IsNullOrWhiteSpace(Responsable) && Responsable.Trim().Length < 3)
        {
            SetErrors(nameof(Responsable), new[] { "El responsable es demasiado corto." });
            return;
        }

        ClearErrors(nameof(Responsable));
    }
}
