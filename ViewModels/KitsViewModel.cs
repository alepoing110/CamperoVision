using System.Collections.ObjectModel;
using System.Globalization;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class KitsViewModel : ValidatableViewModelBase
{
    private readonly KitRepository _repository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IDialogService _dialogService;
    private int _editingId;
    private string _codigo = string.Empty;
    private string _nombre = string.Empty;
    private string _descripcion = string.Empty;
    private string _precioVenta = "0";
    private string _buscar = string.Empty;
    private string _buscarComponente = string.Empty;
    private string _cantidadComponente = "1";
    private string _estado = string.Empty;
    private string _formStatus = "Nuevo kit";
    private string _matchedComponentText = "Busca un producto por codigo de barras, codigo, ID o nombre para agregarlo al kit.";
    private string _matchedComponentStatus = "El stock del kit se calcula con los componentes del almacen seleccionado.";
    private string _componentesText = "Componentes: 0";
    private string _costoReferencialText = "Costo referencial: 0.00";
    private string _precioVentaText = "Precio venta: 0.00";
    private string _margenReferencialText = "Margen referencial: 0.00";
    private string _stockPosibleText = "Stock posible: 0";
    private string _stockRuleText = "Agrega componentes para calcular cuántos kits completos se pueden armar.";
    private WarehouseItem? _selectedWarehouse;
    private KitListItem? _selectedKit;
    private KitComponentDraft? _selectedComponentOption;
    private KitComponentDraft? _selectedComponent;

    public KitsViewModel(KitRepository repository, IInventoryRepository inventoryRepository, IDialogService dialogService)
    {
        _repository = repository;
        _inventoryRepository = inventoryRepository;
        _dialogService = dialogService;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);
        BuscarCommand = new AsyncRelayCommand(LoadAsync);
        NuevoCommand = new RelayCommand(ResetForm);
        BuscarComponenteCommand = new AsyncRelayCommand(LoadComponentOptionsAsync);
        AgregarComponenteCommand = new RelayCommand(AgregarComponente);
        QuitarComponenteCommand = new RelayCommand(QuitarComponente);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => SelectedKit is not null);
    }

    public ObservableCollection<WarehouseItem> Warehouses { get; } = new();
    public ObservableCollection<KitListItem> Kits { get; } = new();
    public ObservableCollection<KitComponentDraft> ComponentOptions { get; } = new();
    public ObservableCollection<KitComponentDraft> Components { get; } = new();

    public AsyncRelayCommand GuardarCommand { get; }
    public AsyncRelayCommand BuscarCommand { get; }
    public RelayCommand NuevoCommand { get; }
    public AsyncRelayCommand BuscarComponenteCommand { get; }
    public RelayCommand AgregarComponenteCommand { get; }
    public RelayCommand QuitarComponenteCommand { get; }
    public AsyncRelayCommand EliminarCommand { get; }

    public string Codigo
    {
        get => _codigo;
        set
        {
            if (SetProperty(ref _codigo, value))
            {
                ValidateCodigo();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

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

    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    public string PrecioVenta
    {
        get => _precioVenta;
        set
        {
            if (SetProperty(ref _precioVenta, value))
            {
                ValidatePrecioVenta();
                UpdateKitSummary();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Buscar
    {
        get => _buscar;
        set
        {
            if (SetProperty(ref _buscar, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync());
            }
        }
    }

    public string BuscarComponente
    {
        get => _buscarComponente;
        set
        {
            if (SetProperty(ref _buscarComponente, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadComponentOptionsAsync());
            }
        }
    }

    public string CantidadComponente
    {
        get => _cantidadComponente;
        set => SetProperty(ref _cantidadComponente, value);
    }

    public string Estado { get => _estado; set => SetProperty(ref _estado, value); }
    public string FormStatus { get => _formStatus; set => SetProperty(ref _formStatus, value); }
    public string MatchedComponentText { get => _matchedComponentText; set => SetProperty(ref _matchedComponentText, value); }
    public string MatchedComponentStatus { get => _matchedComponentStatus; set => SetProperty(ref _matchedComponentStatus, value); }
    public string ComponentesText { get => _componentesText; set => SetProperty(ref _componentesText, value); }
    public string CostoReferencialText { get => _costoReferencialText; set => SetProperty(ref _costoReferencialText, value); }
    public string PrecioVentaText { get => _precioVentaText; set => SetProperty(ref _precioVentaText, value); }
    public string MargenReferencialText { get => _margenReferencialText; set => SetProperty(ref _margenReferencialText, value); }
    public string StockPosibleText { get => _stockPosibleText; set => SetProperty(ref _stockPosibleText, value); }
    public string StockRuleText { get => _stockRuleText; set => SetProperty(ref _stockRuleText, value); }

    public WarehouseItem? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await RefreshWarehouseContextAsync());
            }
        }
    }

    public KitListItem? SelectedKit
    {
        get => _selectedKit;
        set
        {
            if (SetProperty(ref _selectedKit, value))
            {
                EliminarCommand.RaiseCanExecuteChanged();
                if (value is not null)
                {
                    AsyncHelper.FireAndForgetOnUi(async () => await LoadSelectedKitAsync(value.IdKit));
                }
            }
        }
    }

    public KitComponentDraft? SelectedComponentOption
    {
        get => _selectedComponentOption;
        set
        {
            if (SetProperty(ref _selectedComponentOption, value))
            {
                if (value is not null)
                {
                    UpdateMatchedComponent(
                        value,
                        "Coincidencia lista para agregar al kit.",
                        $"Disponible en {SelectedWarehouse?.Nombre ?? "todos los almacenes"}: {value.StockDisponible}. Costo compra: {value.PrecioCompra:N2}. Precio venta: {value.PrecioVenta:N2}.");
                }
                else if (string.IsNullOrWhiteSpace(BuscarComponente))
                {
                    UpdateMatchedComponent(null, string.Empty, "El stock del kit se calcula con los componentes del almacen seleccionado.");
                }
            }
        }
    }

    public KitComponentDraft? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (SetProperty(ref _selectedComponent, value) && value is not null)
            {
                CantidadComponente = value.Cantidad.ToString(CultureInfo.InvariantCulture);
                SelectedComponentOption = ComponentOptions.FirstOrDefault(c => c.IdProducto == value.IdProducto)
                    ?? new KitComponentDraft
                    {
                        IdProducto = value.IdProducto,
                        Codigo = value.Codigo,
                        Nombre = value.Nombre,
                        UnidadMedida = value.UnidadMedida,
                        Cantidad = value.Cantidad,
                        PrecioCompra = value.PrecioCompra,
                        PrecioVenta = value.PrecioVenta,
                        StockDisponible = value.StockDisponible
                    };

                UpdateMatchedComponent(value, "Editando componente seleccionado.", $"Disponible: {value.StockDisponible}. Requerido por kit: {value.Cantidad}. Costo compra: {value.PrecioCompra:N2}.");
            }
        }
    }

    public async Task InitializeAsync()
    {
        ReplaceCollection(Warehouses, await _inventoryRepository.GetWarehousesAsync());

        ResetForm();

        if (Warehouses.Count > 0)
        {
            _selectedWarehouse = Warehouses[0];
            OnPropertyChanged(nameof(SelectedWarehouse));
            await RefreshWarehouseContextAsync();
        }
        else
        {
            await LoadAsync();
            await LoadComponentOptionsAsync();
        }
    }

    public async Task LoadAsync()
    {
        List<KitListItem> items = await _repository.GetAllAsync(SelectedWarehouse?.IdAlmacen, Buscar.Trim());
        ReplaceCollection(Kits, items);
        Estado = items.Count == 0
            ? "No hay kits para mostrar."
            : $"{items.Count} kit(s) cargados.";
    }

    private async Task RefreshWarehouseContextAsync()
    {
        await LoadAsync();
        await LoadComponentOptionsAsync();

        if (_editingId != 0)
        {
            await LoadSelectedKitAsync(_editingId);
        }
        else
        {
            UpdateKitSummary();
        }
    }

    private async Task LoadComponentOptionsAsync()
    {
        List<KitComponentDraft> items = await _repository.GetProductOptionsForBuilderAsync(SelectedWarehouse?.IdAlmacen, BuscarComponente.Trim());
        ReplaceCollection(ComponentOptions, items);

        if (SelectedComponentOption is not null)
        {
            SelectedComponentOption = ComponentOptions.FirstOrDefault(c => c.IdProducto == SelectedComponentOption.IdProducto);
        }

        if (ComponentOptions.Count == 1)
        {
            SelectedComponentOption = ComponentOptions[0];
            return;
        }

        if (string.IsNullOrWhiteSpace(BuscarComponente))
        {
            UpdateMatchedComponent(null, string.Empty, "El stock del kit se calcula con los componentes del almacen seleccionado.");
            return;
        }

        if (ComponentOptions.Count == 0)
        {
            SelectedComponentOption = null;
            UpdateMatchedComponent(null, "No se encontro componente para ese criterio.", "Prueba con codigo de barras, codigo, ID o nombre.");
            return;
        }

        UpdateMatchedComponent(null, $"Se encontraron {ComponentOptions.Count} coincidencias. Elige una en la lista.", "Puedes seguir escribiendo para filtrar mejor.");
    }

    private async Task LoadSelectedKitAsync(int idKit)
    {
        KitUpsertModel? model = await _repository.GetByIdAsync(idKit, SelectedWarehouse?.IdAlmacen);
        if (model is null)
        {
            _dialogService.ShowWarning("El kit seleccionado ya no existe.");
            await LoadAsync();
            ResetForm();
            return;
        }

        _editingId = model.IdKit;
        Codigo = model.Codigo;
        Nombre = model.Nombre;
        Descripcion = model.Descripcion;
        PrecioVenta = model.PrecioVenta.ToString(CultureInfo.InvariantCulture);
        Components.Clear();
        foreach (KitComponentDraft component in model.Componentes)
        {
            Components.Add(new KitComponentDraft
            {
                IdProducto = component.IdProducto,
                Codigo = component.Codigo,
                Nombre = component.Nombre,
                UnidadMedida = component.UnidadMedida,
                PrecioCompra = component.PrecioCompra,
                PrecioVenta = component.PrecioVenta,
                StockDisponible = component.StockDisponible,
                Cantidad = component.Cantidad
            });
        }

        FormStatus = $"Editando kit ID {_editingId}: {model.Nombre}";
        ClearAllErrors();
        SelectedComponent = null;
        UpdateKitSummary();
        GuardarCommand.RaiseCanExecuteChanged();
    }

    private async Task GuardarAsync()
    {
        ValidateForm();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos del kit antes de guardar.");
            return;
        }

        if (!decimal.TryParse(PrecioVenta, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal precioVenta) &&
            !decimal.TryParse(PrecioVenta, out precioVenta))
        {
            _dialogService.ShowWarning("Precio de venta invalido.");
            return;
        }

        KitUpsertModel model = new()
        {
            IdKit = _editingId,
            Codigo = Codigo.Trim(),
            Nombre = Nombre.Trim(),
            Descripcion = Descripcion.Trim(),
            PrecioVenta = precioVenta,
            Activo = true,
            Componentes = Components.Select(component => new KitComponentDraft
            {
                IdProducto = component.IdProducto,
                Cantidad = component.Cantidad
            }).ToList()
        };

        (bool codigoExiste, bool nombreExiste) = await _repository.CheckDuplicatesAsync(model);
        if (codigoExiste)
        {
            SetErrors(nameof(Codigo), new[] { "Ya existe un kit con ese codigo." });
        }

        if (nombreExiste)
        {
            SetErrors(nameof(Nombre), new[] { "Ya existe un kit con ese nombre." });
        }

        if (HasErrors)
        {
            _dialogService.ShowWarning("No se puede guardar porque hay datos repetidos o incompletos en el kit.");
            return;
        }

        bool isNew = _editingId == 0;
        await _repository.SaveAsync(model);
        await LoadAsync();
        Estado = isNew ? "Kit creado correctamente." : "Kit actualizado correctamente.";
        ResetForm();
    }

    private async Task EliminarAsync()
    {
        if (SelectedKit is null)
        {
            return;
        }

        await _repository.DeleteAsync(SelectedKit.IdKit);
        await LoadAsync();
        Estado = "Kit eliminado correctamente.";
        ResetForm();
    }

    private void AgregarComponente()
    {
        ClearErrors(nameof(CantidadComponente));
        ValidateCantidadComponente();
        bool quantityHasErrors = GetErrors(nameof(CantidadComponente)).Cast<object>().Any();
        if (quantityHasErrors)
        {
            _dialogService.ShowWarning("Corrige la cantidad del componente.");
            return;
        }

        if (SelectedComponentOption is null)
        {
            _dialogService.ShowWarning("Selecciona un producto para agregar al kit.");
            return;
        }

        if (!int.TryParse(CantidadComponente, out int cantidad) || cantidad <= 0)
        {
            _dialogService.ShowWarning("Ingresa una cantidad valida.");
            return;
        }

        KitComponentDraft? existing = Components.FirstOrDefault(c => c.IdProducto == SelectedComponentOption.IdProducto);
        if (SelectedComponent is not null && SelectedComponent.IdProducto == SelectedComponentOption.IdProducto)
        {
            SelectedComponent.Cantidad = cantidad;
            SelectedComponent.PrecioCompra = SelectedComponentOption.PrecioCompra;
            SelectedComponent.PrecioVenta = SelectedComponentOption.PrecioVenta;
            SelectedComponent.StockDisponible = SelectedComponentOption.StockDisponible;
        }
        else if (existing is not null)
        {
            existing.Cantidad += cantidad;
            existing.PrecioCompra = SelectedComponentOption.PrecioCompra;
            existing.PrecioVenta = SelectedComponentOption.PrecioVenta;
            existing.StockDisponible = SelectedComponentOption.StockDisponible;
        }
        else
        {
            Components.Add(new KitComponentDraft
            {
                IdProducto = SelectedComponentOption.IdProducto,
                Codigo = SelectedComponentOption.Codigo,
                Nombre = SelectedComponentOption.Nombre,
                UnidadMedida = SelectedComponentOption.UnidadMedida,
                PrecioCompra = SelectedComponentOption.PrecioCompra,
                PrecioVenta = SelectedComponentOption.PrecioVenta,
                StockDisponible = SelectedComponentOption.StockDisponible,
                Cantidad = cantidad
            });
        }

        SelectedComponent = null;
        CantidadComponente = "1";
        FormStatus = _editingId == 0 ? "Nuevo kit" : $"Editando kit ID {_editingId}: {Nombre}";
        UpdateKitSummary();
        GuardarCommand.RaiseCanExecuteChanged();
    }

    private void QuitarComponente()
    {
        if (SelectedComponent is null)
        {
            return;
        }

        Components.Remove(SelectedComponent);
        SelectedComponent = null;
        CantidadComponente = "1";
        UpdateKitSummary();
        GuardarCommand.RaiseCanExecuteChanged();
    }

    private void ResetForm()
    {
        _editingId = 0;
        _selectedKit = null;
        OnPropertyChanged(nameof(SelectedKit));
        Codigo = string.Empty;
        Nombre = string.Empty;
        Descripcion = string.Empty;
        PrecioVenta = "0";
        Components.Clear();
        SelectedComponent = null;
        SelectedComponentOption = null;
        CantidadComponente = "1";
        ClearAllErrors();
        FormStatus = "Nuevo kit";
        UpdateMatchedComponent(null, string.Empty, "El stock del kit se calcula con los componentes del almacen seleccionado.");
        UpdateKitSummary();
        GuardarCommand.RaiseCanExecuteChanged();
        EliminarCommand.RaiseCanExecuteChanged();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Codigo) && !string.IsNullOrWhiteSpace(Nombre) && Components.Count > 0;

    private void ValidateForm()
    {
        ValidateCodigo();
        ValidateNombre();
        ValidatePrecioVenta();

        if (Components.Count == 0)
        {
            SetErrors(nameof(Components), new[] { "Agrega al menos un componente al kit." });
        }
        else
        {
            ClearErrors(nameof(Components));
        }
    }

    private void ValidateCodigo()
    {
        if (string.IsNullOrWhiteSpace(Codigo))
        {
            SetErrors(nameof(Codigo), new[] { "El codigo es obligatorio." });
            return;
        }

        ClearErrors(nameof(Codigo));
    }

    private void ValidateNombre()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            SetErrors(nameof(Nombre), new[] { "El nombre es obligatorio." });
            return;
        }

        ClearErrors(nameof(Nombre));
    }

    private void ValidatePrecioVenta()
    {
        bool parsed = decimal.TryParse(PrecioVenta, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value)
                      || decimal.TryParse(PrecioVenta, out value);
        if (!parsed || value < 0)
        {
            SetErrors(nameof(PrecioVenta), new[] { "Ingresa un precio de venta valido." });
            return;
        }

        ClearErrors(nameof(PrecioVenta));
    }

    private void ValidateCantidadComponente()
    {
        if (!int.TryParse(CantidadComponente, out int value) || value <= 0)
        {
            SetErrors(nameof(CantidadComponente), new[] { "Ingresa una cantidad valida para el componente." });
            return;
        }

        ClearErrors(nameof(CantidadComponente));
    }

    private void UpdateMatchedComponent(KitComponentDraft? component, string message, string status)
    {
        if (component is null)
        {
            MatchedComponentText = string.IsNullOrWhiteSpace(message)
                ? "Busca un producto por codigo de barras, codigo, ID o nombre para agregarlo al kit."
                : message;
            MatchedComponentStatus = status;
            return;
        }

        MatchedComponentText = $"{component.Codigo} | {component.Nombre} | Unidad: {component.UnidadMedida}";
        MatchedComponentStatus = string.IsNullOrWhiteSpace(status)
            ? $"Stock disponible: {component.StockDisponible}. Costo compra: {component.PrecioCompra:N2}. Precio venta: {component.PrecioVenta:N2}."
            : status;
    }

    private void UpdateKitSummary()
    {
        int totalTipos = Components.Count;
        int totalUnidades = Components.Sum(component => component.Cantidad);
        decimal costoReferencial = Components.Sum(component => component.CostoReferencial);
        decimal precioVenta = ParseNonNegativeDecimal(PrecioVenta);
        decimal margen = precioVenta - costoReferencial;

        ComponentesText = $"Componentes: {totalTipos} tipo(s) / {totalUnidades} unidad(es)";
        CostoReferencialText = $"Costo referencial: {costoReferencial:N2}";
        PrecioVentaText = $"Precio venta: {precioVenta:N2}";
        MargenReferencialText = $"Margen referencial: {margen:N2}";

        if (Components.Count == 0)
        {
            StockPosibleText = "Stock posible: 0";
            StockRuleText = "El kit no tiene stock propio. Agrega componentes para calcular cuántos kits completos se pueden armar con el almacén seleccionado.";
            return;
        }

        var limitingData = Components
            .Select(component => new
            {
                Component = component,
                KitsPosibles = component.Cantidad <= 0 ? 0 : component.StockDisponible / component.Cantidad
            })
            .OrderBy(data => data.KitsPosibles)
            .ThenBy(data => data.Component.Nombre)
            .First();

        int stockPosible = limitingData.KitsPosibles;
        StockPosibleText = $"Stock posible: {stockPosible}";

        if (stockPosible <= 0)
        {
            StockRuleText = $"Stock 0: el kit no maneja inventario propio y no se puede armar ninguna unidad completa porque el componente limitante es {limitingData.Component.Nombre} (disponible {limitingData.Component.StockDisponible}, requerido {limitingData.Component.Cantidad} por kit).";
            return;
        }

        StockRuleText = $"Stock del kit = {stockPosible}. Se calcula por componentes del almacén seleccionado. El componente limitante actual es {limitingData.Component.Nombre} (disponible {limitingData.Component.StockDisponible}, requerido {limitingData.Component.Cantidad} por kit).";
    }

    private static decimal ParseNonNegativeDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed)
            || decimal.TryParse(value, out parsed)
            ? Math.Max(parsed, 0)
            : 0;
    }
}
