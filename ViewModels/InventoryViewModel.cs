using System.Collections.ObjectModel;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class InventoryViewModel : ValidatableViewModelBase
{
    private readonly IInventoryRepository _repository;
    private readonly IInventoryService _inventoryService;
    private readonly IDialogService _dialogService;
    private readonly UserSession _currentUser;
    private readonly List<InventoryMovementItem> _kardexCache = new();
    private int _productsLoadVersion;
    private int _kardexLoadVersion;
    private WarehouseItem? _selectedWarehouse;
    private ProductOption? _selectedProduct;
    private ProductOption? _matchedProduct;
    private InventoryListItem? _selectedInventoryItem;
    private string _productSearch = string.Empty;
    private string _inventorySearch = string.Empty;
    private string _kardexSearch = string.Empty;
    private int _productoId;
    private int _cantidad = 1;
    private decimal _precio;
    private int _stockActual;
    private string _statusMessage = "Listo";
    private string _movementReason = "Movimiento manual";
    private bool _isLoading;
    private string _targetStock = "0";
    private string _selectedMovementType = "Todos";
    private string _matchedProductText = "Busca por codigo de barras, codigo, ID o nombre.";
    private string _matchedProductStatus = string.Empty;

    public InventoryViewModel(IInventoryRepository repository, IInventoryService inventoryService, IDialogService dialogService, UserSession currentUser)
    {
        _repository = repository;
        _inventoryService = inventoryService;
        _dialogService = dialogService;
        _currentUser = currentUser;

        MovementTypes = new ObservableCollection<string>(new[] { "Todos", "entrada", "salida" });
        ComprarCommand = new AsyncRelayCommand(() => RegistrarMovimientoAsync("entrada"), CanExecuteMovement);
        VenderCommand = new AsyncRelayCommand(() => RegistrarMovimientoAsync("salida"), CanExecuteMovement);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SearchInventoryCommand = new AsyncRelayCommand(LoadInventoryAsync);
        ApplyKardexFiltersCommand = new RelayCommand(ApplyKardexFilters);
        AdjustStockCommand = new AsyncRelayCommand(AdjustStockAsync, CanAdjustStock);
    }

    public ObservableCollection<WarehouseItem> Warehouses { get; } = new();
    public ObservableCollection<ProductOption> Products { get; } = new();
    public ObservableCollection<InventoryListItem> InventoryItems { get; } = new();
    public ObservableCollection<InventoryMovementItem> KardexItems { get; } = new();
    public ObservableCollection<string> MovementTypes { get; }

    public AsyncRelayCommand ComprarCommand { get; }
    public AsyncRelayCommand VenderCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SearchInventoryCommand { get; }
    public AsyncRelayCommand AdjustStockCommand { get; }
    public RelayCommand ApplyKardexFiltersCommand { get; }

    public WarehouseItem? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await RefreshWarehouseDataAsync());
            }
        }
    }

    public ProductOption? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                ProductoId = value?.IdProducto ?? 0;
                Precio = value?.PrecioVenta ?? 0;
                AsyncHelper.FireAndForgetOnUi(async () => await UpdateStockAndKardexAsync());
                RaiseCommandState();
            }
        }
    }

    public InventoryListItem? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set
        {
            if (SetProperty(ref _selectedInventoryItem, value) && value is not null)
            {
                ProductSearch = value.CodigoBarras;
                SelectedProduct = Products.FirstOrDefault(p => p.IdProducto == value.IdProducto) ?? SelectedProduct;
                StockActual = value.Cantidad;
                TargetStock = value.Cantidad.ToString();
                MovementReason = $"Correccion manual de inventario para {value.Producto}";
                RaiseCommandState();
            }
        }
    }

    public string ProductSearch
    {
        get => _productSearch;
        set
        {
            if (SetProperty(ref _productSearch, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadProductsAsync(resolveSelection: true));
            }
        }
    }

    public string InventorySearch
    {
        get => _inventorySearch;
        set => SetProperty(ref _inventorySearch, value);
    }

    public string KardexSearch
    {
        get => _kardexSearch;
        set
        {
            if (SetProperty(ref _kardexSearch, value))
            {
                ApplyKardexFilters();
            }
        }
    }

    public int ProductoId
    {
        get => _productoId;
        set
        {
            if (SetProperty(ref _productoId, value))
            {
                RaiseCommandState();
            }
        }
    }

    public int Cantidad
    {
        get => _cantidad;
        set
        {
            if (SetProperty(ref _cantidad, value))
            {
                ValidateCantidad();
                RaiseCommandState();
            }
        }
    }

    public decimal Precio
    {
        get => _precio;
        set
        {
            if (SetProperty(ref _precio, value))
            {
                ValidatePrecio();
                RaiseCommandState();
            }
        }
    }

    public int StockActual
    {
        get => _stockActual;
        set => SetProperty(ref _stockActual, value);
    }

    public string MovementReason
    {
        get => _movementReason;
        set
        {
            if (SetProperty(ref _movementReason, value))
            {
                ValidateMotivo();
                RaiseCommandState();
            }
        }
    }

    public string TargetStock
    {
        get => _targetStock;
        set
        {
            if (SetProperty(ref _targetStock, value))
            {
                ValidateTargetStock();
                RaiseCommandState();
            }
        }
    }

    public string SelectedMovementType
    {
        get => _selectedMovementType;
        set
        {
            if (SetProperty(ref _selectedMovementType, value))
            {
                ApplyKardexFilters();
            }
        }
    }

    public string MatchedProductText
    {
        get => _matchedProductText;
        set => SetProperty(ref _matchedProductText, value);
    }

    public string MatchedProductStatus
    {
        get => _matchedProductStatus;
        set => SetProperty(ref _matchedProductStatus, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Cargando almacenes y productos...";

            ReplaceCollection(Warehouses, await _repository.GetWarehousesAsync());

            if (SelectedWarehouse is null && Warehouses.Count > 0)
            {
                SelectedWarehouse = Warehouses[0];
            }
            else
            {
                await RefreshWarehouseDataAsync();
            }

            StatusMessage = "Inventario listo.";
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo cargar el inventario.";
            _dialogService.ShowError(ex.Message, "Error cargando inventario");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshWarehouseDataAsync()
    {
        if (SelectedWarehouse is null)
        {
            return;
        }

        _inventoryService.WarehouseId = SelectedWarehouse.IdAlmacen;
        _inventoryService.UserId = _currentUser.IdUsuario;
        _inventoryService.DefaultReason = MovementReason;

        await LoadProductsAsync();
        await LoadInventoryAsync();
        await LoadKardexAsync();
    }

    private async Task LoadProductsAsync(bool resolveSelection = false)
    {
        if (SelectedWarehouse is null)
        {
            return;
        }

        int loadVersion = ++_productsLoadVersion;
        List<ProductOption> items = await _repository.GetProductsAsync(SelectedWarehouse.IdAlmacen, ProductSearch.Trim());
        if (loadVersion != _productsLoadVersion)
        {
            return;
        }

        Products.Clear();
        ReplaceCollection(Products, items);

        if (SelectedProduct is not null)
        {
            SelectedProduct = Products.FirstOrDefault(p => p.IdProducto == SelectedProduct.IdProducto);
        }

        if (SelectedProduct is null && Products.Count > 0)
        {
            SelectedProduct = Products[0];
        }

        if (resolveSelection)
        {
            ResolveProductSelection(ProductSearch.Trim());
        }
        else if (SelectedProduct is not null)
        {
            UpdateMatchedProduct(SelectedProduct, "Producto seleccionado.");
        }
    }

    private async Task LoadInventoryAsync()
    {
        List<InventoryListItem> items = await _repository.GetAllAsync(SelectedWarehouse?.IdAlmacen, InventorySearch.Trim());
        ReplaceCollection(InventoryItems, items);
        StatusMessage = $"Inventario filtrado: {items.Count} registro(s).";
    }

    private async Task LoadKardexAsync()
    {
        int loadVersion = ++_kardexLoadVersion;
        int? productId = SelectedProduct?.IdProducto;
        List<InventoryMovementItem> kardex = await _repository.GetKardexAsync(SelectedWarehouse?.IdAlmacen, productId, 300);
        if (loadVersion != _kardexLoadVersion)
        {
            return;
        }

        _kardexCache.Clear();
        _kardexCache.AddRange(kardex);
        ApplyKardexFilters();
    }

    private void ApplyKardexFilters()
    {
        IEnumerable<InventoryMovementItem> filtered = _kardexCache
            .GroupBy(item => item.IdMovimiento)
            .Select(group => group.First());

        if (!string.Equals(SelectedMovementType, "Todos", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(item => item.Tipo.Equals(SelectedMovementType, StringComparison.OrdinalIgnoreCase));
        }

        string search = KardexSearch.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(item =>
                item.CodigoBarras.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Codigo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Producto.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Usuario.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Motivo.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        ReplaceCollection(KardexItems, filtered);
    }

    private async Task UpdateStockAndKardexAsync()
    {
        if (SelectedWarehouse is null || SelectedProduct is null)
        {
            StockActual = 0;
            UpdateMatchedProduct(null, string.Empty);
            return;
        }

        StockActual = await _repository.GetCurrentStockAsync(SelectedProduct.IdProducto, SelectedWarehouse.IdAlmacen);
        TargetStock = StockActual.ToString();
        UpdateMatchedProduct(SelectedProduct, "Producto listo para movimiento.");
        await LoadKardexAsync();
    }

    private bool CanExecuteMovement()
    {
        return SelectedWarehouse is not null && ProductoId > 0 && Cantidad > 0 && !HasErrors;
    }

    private bool CanAdjustStock()
    {
        return SelectedWarehouse is not null && SelectedInventoryItem is not null && !HasErrors;
    }

    private async Task RegistrarMovimientoAsync(string tipo)
    {
        try
        {
            if (!EnsureResolvedProductSelection())
            {
                return;
            }

            if (SelectedProduct is null || SelectedWarehouse is null)
            {
                _dialogService.ShowWarning("Selecciona un almacen y un producto antes de continuar.");
                return;
            }

            ValidateCantidad();
            ValidatePrecio();
            ValidateMotivo();
            if (HasErrors)
            {
                _dialogService.ShowWarning("Corrige cantidad, precio o motivo antes de registrar el movimiento.");
                return;
            }

            IsLoading = true;
            StatusMessage = $"Registrando {tipo}...";
            _inventoryService.WarehouseId = SelectedWarehouse.IdAlmacen;
            _inventoryService.UserId = _currentUser.IdUsuario;
            _inventoryService.DefaultReason = MovementReason;

            await _inventoryService.RegistrarMovimiento(SelectedProduct.IdProducto, Cantidad, Precio, tipo);

            StockActual = await _repository.GetCurrentStockAsync(SelectedProduct.IdProducto, SelectedWarehouse.IdAlmacen);
            TargetStock = StockActual.ToString();
            await LoadProductsAsync();
            await LoadInventoryAsync();
            await LoadKardexAsync();

            StatusMessage = $"Movimiento de {tipo} registrado correctamente.";
            _dialogService.ShowInfo($"Movimiento de {tipo} registrado para {SelectedProduct.Nombre}.", "Operacion completada");
            Cantidad = 1;
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo registrar la {tipo}.";
            _dialogService.ShowError(ex.Message, "Error de movimiento");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AdjustStockAsync()
    {
        try
        {
            if (!EnsureResolvedProductSelection(allowInventorySelection: true))
            {
                return;
            }

            if (SelectedWarehouse is null || SelectedInventoryItem is null)
            {
                _dialogService.ShowWarning("Selecciona un registro de inventario para ajustar.");
                return;
            }

            ValidateTargetStock();
            ValidateMotivo();
            if (HasErrors)
            {
                _dialogService.ShowWarning("Corrige el stock destino o el motivo antes de aplicar el ajuste.");
                return;
            }

            if (!int.TryParse(TargetStock, out int targetStock))
            {
                _dialogService.ShowWarning("El stock corregido es invalido.");
                return;
            }

            int difference = targetStock - StockActual;
            if (difference == 0)
            {
                _dialogService.ShowWarning("El stock corregido es igual al stock actual. No hay cambios para aplicar.");
                return;
            }

            IsLoading = true;
            StatusMessage = "Aplicando ajuste manual...";

            await _repository.AdjustStockAsync(new InventoryAdjustmentRequest
            {
                IdProducto = SelectedInventoryItem.IdProducto,
                IdAlmacen = SelectedWarehouse.IdAlmacen,
                IdUsuario = _currentUser.IdUsuario,
                Cantidad = difference,
                Motivo = $"Ajuste manual | {MovementReason.Trim()} | Stock actual: {StockActual} | Nuevo stock: {targetStock}"
            });

            await LoadProductsAsync();
            await LoadInventoryAsync();
            SelectedProduct = Products.FirstOrDefault(p => p.IdProducto == SelectedInventoryItem.IdProducto);
            StockActual = await _repository.GetCurrentStockAsync(SelectedInventoryItem.IdProducto, SelectedWarehouse.IdAlmacen);
            TargetStock = StockActual.ToString();
            await LoadKardexAsync();

            StatusMessage = "Ajuste de inventario aplicado correctamente.";
            _dialogService.ShowInfo("Se aplico la correccion de stock correctamente.", "Ajuste completado");
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo aplicar el ajuste.";
            _dialogService.ShowError(ex.Message, "Error de ajuste");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RaiseCommandState()
    {
        ComprarCommand.RaiseCanExecuteChanged();
        VenderCommand.RaiseCanExecuteChanged();
        AdjustStockCommand.RaiseCanExecuteChanged();
    }

    private void ValidateCantidad()
    {
        if (Cantidad <= 0)
        {
            SetErrors(nameof(Cantidad), new[] { "La cantidad debe ser mayor a cero." });
            return;
        }

        ClearErrors(nameof(Cantidad));
    }

    private void ValidatePrecio()
    {
        if (Precio < 0)
        {
            SetErrors(nameof(Precio), new[] { "El precio no puede ser negativo." });
            return;
        }

        ClearErrors(nameof(Precio));
    }

    private void ValidateMotivo()
    {
        if (string.IsNullOrWhiteSpace(MovementReason))
        {
            SetErrors(nameof(MovementReason), new[] { "Ingresa un motivo para el movimiento." });
            return;
        }

        ClearErrors(nameof(MovementReason));
    }

    private void ValidateTargetStock()
    {
        if (!int.TryParse(TargetStock, out int targetStock) || targetStock < 0)
        {
            SetErrors(nameof(TargetStock), new[] { "Ingresa un stock corregido valido." });
            return;
        }

        ClearErrors(nameof(TargetStock));
    }

    private void ResolveProductSelection(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _matchedProduct = null;
            UpdateMatchedProduct(SelectedProduct, SelectedProduct is null ? string.Empty : "Producto seleccionado manualmente.");
            return;
        }

        ProductOption? exactBarcode = Products.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.CodigoBarras) && p.CodigoBarras.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactBarcode is not null) { SelectMatchedProduct(exactBarcode, "Coincidencia por codigo de barras."); return; }

        ProductOption? exactCode = Products.FirstOrDefault(p => p.Codigo.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactCode is not null) { SelectMatchedProduct(exactCode, "Coincidencia por codigo interno."); return; }

        ProductOption? exactId = Products.FirstOrDefault(p => p.IdProducto.ToString() == query);
        if (exactId is not null) { SelectMatchedProduct(exactId, "Coincidencia por ID del producto."); return; }

        ProductOption? exactName = Products.FirstOrDefault(p => p.Nombre.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactName is not null) { SelectMatchedProduct(exactName, "Coincidencia exacta por nombre."); return; }

        List<ProductOption> nameMatches = Products.Where(p => p.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (nameMatches.Count == 1)
        {
            SelectMatchedProduct(nameMatches[0], "Coincidencia unica por nombre.");
            return;
        }

        _matchedProduct = null;
        if (nameMatches.Count > 0)
        {
            SelectedProduct = nameMatches[0];
        }

        UpdateMatchedProduct(null, nameMatches.Count > 1
            ? $"Se encontraron {nameMatches.Count} coincidencias. Sigue escribiendo o elige manualmente el producto correcto."
            : "No se encontro producto con ese criterio.");
    }

    private void SelectMatchedProduct(ProductOption product, string message)
    {
        _matchedProduct = product;
        SelectedProduct = product;
        UpdateMatchedProduct(product, message);
    }

    private void UpdateMatchedProduct(ProductOption? product, string message)
    {
        if (product is null)
        {
            MatchedProductText = string.IsNullOrWhiteSpace(message) ? "Busca por codigo de barras, codigo, ID o nombre." : message;
            MatchedProductStatus = string.Empty;
            return;
        }

        string barcodePart = string.IsNullOrWhiteSpace(product.CodigoBarras) ? "Sin codigo de barras" : $"CB: {product.CodigoBarras}";
        MatchedProductText = $"{barcodePart} | {product.Codigo} | ID {product.IdProducto} | {product.Nombre}";
        MatchedProductStatus = $"Stock: {product.StockDisponible} | Precio: {product.PrecioVenta:N2} Bs | {message}";
    }

    private bool EnsureResolvedProductSelection(bool allowInventorySelection = false)
    {
        string query = ProductSearch.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            ResolveProductSelection(query);
            if (_matchedProduct is null && !string.IsNullOrWhiteSpace(query))
            {
                _dialogService.ShowWarning("La busqueda del producto no es suficientemente precisa. Verifica codigo de barras, codigo, ID o selecciona manualmente el producto correcto.");
                return false;
            }
        }

        if (SelectedProduct is null)
        {
            if (allowInventorySelection && SelectedInventoryItem is not null)
            {
                SelectedProduct = Products.FirstOrDefault(p => p.IdProducto == SelectedInventoryItem.IdProducto);
            }

            if (SelectedProduct is null)
            {
                _dialogService.ShowWarning("No hay un producto valido seleccionado para el movimiento.");
                return false;
            }
        }

        return true;
    }
}
