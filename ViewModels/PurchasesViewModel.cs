using System.Collections.ObjectModel;
using System.Globalization;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class PurchasesViewModel : ValidatableViewModelBase
{
    private readonly OrdenCompraRepository _repository;
    private readonly ProveedorRepository _proveedorRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IDialogService _dialogService;
    private readonly UserSession _currentUser;
    private string _status = "Listo";
    private WarehouseItem? _selectedWarehouse;
    private ProveedorOption? _selectedProvider;
    private ProductOption? _selectedProduct;
    private OrdenCompraListItem? _selectedOrder;
    private DetalleOrdenCompraDraft? _selectedDraftItem;
    private string _searchText = string.Empty;
    private string _quantity = "1";
    private string _price = "0";
    private string _observations = string.Empty;
    private string _matchedProductText = "Busca por codigo de barras, codigo, ID o nombre.";
    private string _matchedProductStatus = string.Empty;
    private bool _isProductDropdownOpen;
    private ProductOption? _matchedProduct;

    public PurchasesViewModel(OrdenCompraRepository repository, ProveedorRepository proveedorRepository, IInventoryRepository inventoryRepository, IDialogService dialogService, UserSession currentUser)
    {
        _repository = repository;
        _proveedorRepository = proveedorRepository;
        _inventoryRepository = inventoryRepository;
        _dialogService = dialogService;
        _currentUser = currentUser;
        NuevaOrdenCommand = new RelayCommand(ResetForm);
        AddItemCommand = new RelayCommand(AddItem);
        RemoveItemCommand = new RelayCommand(RemoveItem);
        SaveOrderCommand = new AsyncRelayCommand(SaveOrderAsync);
        ReceiveOrderCommand = new AsyncRelayCommand(ReceiveOrderAsync, () => SelectedOrder is not null && SelectedOrder.Estado != "recibida");
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ObservableCollection<OrdenCompraListItem> Orders { get; } = new();
    public ObservableCollection<WarehouseItem> Warehouses { get; } = new();
    public ObservableCollection<ProveedorOption> Providers { get; } = new();
    public ObservableCollection<ProductOption> Products { get; } = new();
    public ObservableCollection<DetalleOrdenCompraDraft> DraftItems { get; } = new();

    public RelayCommand NuevaOrdenCommand { get; }
    public RelayCommand AddItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public AsyncRelayCommand SaveOrderCommand { get; }
    public AsyncRelayCommand ReceiveOrderCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadProductsAsync(value.Trim()));
            }
        }
    }

    public string Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }
    public string Price { get => _price; set => SetProperty(ref _price, value); }
    public string Observations { get => _observations; set => SetProperty(ref _observations, value); }
    public string MatchedProductText { get => _matchedProductText; set => SetProperty(ref _matchedProductText, value); }
    public string MatchedProductStatus { get => _matchedProductStatus; set => SetProperty(ref _matchedProductStatus, value); }
    public bool IsProductDropdownOpen { get => _isProductDropdownOpen; set => SetProperty(ref _isProductDropdownOpen, value); }
    public string DraftTotalText => $"Total orden: {DraftItems.Sum(x => x.Subtotal):N2}";

    public WarehouseItem? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set => SetProperty(ref _selectedWarehouse, value);
    }

    public ProveedorOption? SelectedProvider
    {
        get => _selectedProvider;
        set => SetProperty(ref _selectedProvider, value);
    }

    public ProductOption? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value) && value is not null)
            {
                Price = value.PrecioVenta.ToString("N2", CultureInfo.InvariantCulture);
                UpdateMatchedProduct(value, "Producto seleccionado para compra.");
            }
        }
    }

    public OrdenCompraListItem? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetProperty(ref _selectedOrder, value))
            {
                ReceiveOrderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DetalleOrdenCompraDraft? SelectedDraftItem
    {
        get => _selectedDraftItem;
        set => SetProperty(ref _selectedDraftItem, value);
    }

    public async Task LoadAsync()
    {
        try
        {
            ClearAllErrors();
            ReplaceCollection(Providers, await _proveedorRepository.GetOptionsAsync());
            ReplaceCollection(Warehouses, await _inventoryRepository.GetWarehousesAsync());
            ReplaceCollection(Products, await _inventoryRepository.GetProductsAsync());
            ReplaceCollection(Orders, await _repository.GetAllAsync());

            if (SelectedWarehouse is null && Warehouses.Count > 0)
            {
                SelectedWarehouse = Warehouses[0];
            }

            if (SelectedProvider is null && Providers.Count > 0)
            {
                SelectedProvider = Providers[0];
            }

            Status = $"Cargadas {Orders.Count} ordenes de compra";
            UpdateMatchedProduct(null, string.Empty);
            OnPropertyChanged(nameof(DraftTotalText));
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
            _dialogService.ShowError($"No se pudo cargar el modulo de compras.\n\nDetalle: {ex.Message}");
        }
    }

    private async Task LoadProductsAsync(string? search = null)
    {
        string criteria = search ?? SearchText.Trim();
        ReplaceCollection(Products, await _inventoryRepository.GetProductsAsync(search: criteria));

        if (Products.Count > 0 && SelectedProduct is not null)
        {
            SelectedProduct = Products.FirstOrDefault(p => p.IdProducto == SelectedProduct.IdProducto);
        }

        ResolveProduct(criteria);
    }
    private void ResetForm()
    {
        ClearAllErrors();
        DraftItems.Clear();
        SearchText = string.Empty;
        Quantity = "1";
        Price = "0";
        Observations = string.Empty;
        SelectedProduct = null;
        SelectedProvider = Providers.FirstOrDefault();
        SelectedWarehouse = Warehouses.FirstOrDefault();
        SelectedDraftItem = null;
        _matchedProduct = null;
        UpdateMatchedProduct(null, string.Empty);
        Status = "Formulario limpio para una nueva orden.";
        OnPropertyChanged(nameof(DraftTotalText));
    }

    private void ResolveProduct(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _matchedProduct = null;
            IsProductDropdownOpen = false;
            UpdateMatchedProduct(null, string.Empty);
            return;
        }

        ProductOption? exactBarcode = Products.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.CodigoBarras) && p.CodigoBarras.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactBarcode is not null)
        {
            SelectMatchedProduct(exactBarcode, "Coincidencia por codigo de barras.");
            return;
        }

        ProductOption? exactCode = Products.FirstOrDefault(p => p.Codigo.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactCode is not null)
        {
            SelectMatchedProduct(exactCode, "Coincidencia por codigo interno.");
            return;
        }

        ProductOption? exactId = Products.FirstOrDefault(p => p.IdProducto.ToString() == query);
        if (exactId is not null)
        {
            SelectMatchedProduct(exactId, "Coincidencia por ID del producto.");
            return;
        }

        ProductOption? exactName = Products.FirstOrDefault(p => p.Nombre.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactName is not null)
        {
            SelectMatchedProduct(exactName, "Coincidencia exacta por nombre.");
            return;
        }

        List<ProductOption> nameMatches = Products.Where(p => p.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (nameMatches.Count == 1)
        {
            SelectMatchedProduct(nameMatches[0], "Coincidencia unica por nombre.");
            return;
        }

        _matchedProduct = null;
        SelectedProduct = nameMatches.FirstOrDefault();
        IsProductDropdownOpen = nameMatches.Count > 1;
        UpdateMatchedProduct(null, nameMatches.Count > 1 ? $"Se encontraron {nameMatches.Count} coincidencias por nombre. Sigue escribiendo o elige manualmente." : "No se encontro producto para ese criterio.");
    }

    private void SelectMatchedProduct(ProductOption product, string message)
    {
        _matchedProduct = product;
        SelectedProduct = product;
        IsProductDropdownOpen = false;
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
        MatchedProductStatus = $"Stock actual: {product.StockDisponible} | Precio ref.: {product.PrecioVenta:N2} Bs | {message}";
    }

    private void AddItem()
    {
        ValidateQuantity();
        ValidatePrice();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Verifica todos los campos del item antes de agregarlo o cambiarlo");
            return;
        }

        ProductOption? product = _matchedProduct ?? SelectedProduct;
        if (product is null)
        {
            _dialogService.ShowWarning("Selecciona un producto.");
            return;
        }

        if (!int.TryParse(Quantity, out int quantity) || quantity <= 0)
        {
            _dialogService.ShowWarning("Ingresa una cantidad valida.");
            return;
        }

        if (!decimal.TryParse(Price, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) &&
            !decimal.TryParse(Price, out price))
        {
            _dialogService.ShowWarning("Ingresa un precio valido.");
            return;
        }

        DraftItems.Add(new DetalleOrdenCompraDraft
        {
            IdProducto = product.IdProducto,
            Codigo = product.Codigo,
            Producto = product.Nombre,
            Cantidad = quantity,
            PrecioUnitario = price
        });

        SearchText = string.Empty;
        Quantity = "1";
        Price = product.PrecioVenta.ToString("N2", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(DraftTotalText));
        UpdateMatchedProduct(null, "Busca el siguiente producto.");
        Status = $"Item agregado. Total items: {DraftItems.Count}";
    }

    private void RemoveItem()
    {
        if (SelectedDraftItem is null)
        {
            return;
        }

        DraftItems.Remove(SelectedDraftItem);
        SelectedDraftItem = null;
        OnPropertyChanged(nameof(DraftTotalText));
    }

    private async Task SaveOrderAsync()
    {
        if (SelectedProvider is null || SelectedWarehouse is null)
        {
            _dialogService.ShowWarning("Selecciona proveedor y almacen.");
            return;
        }

        if (DraftItems.Count == 0)
        {
            _dialogService.ShowWarning("Agrega al menos un item a la orden.");
            return;
        }

        int orderId = await _repository.CreateAsync(new PurchaseCreateRequest
        {
            IdProveedor = SelectedProvider.IdProveedor,
            IdUsuario = _currentUser.IdUsuario,
            IdAlmacen = SelectedWarehouse.IdAlmacen,
            Estado = "enviada",
            Observaciones = Observations,
            Items = DraftItems.ToList()
        });

        _dialogService.ShowInfo($"Orden de compra #{orderId} guardada correctamente.");
        ResetForm();
        await LoadAsync();
    }

    private async Task ReceiveOrderAsync()
    {
        if (SelectedOrder is null)
        {
            _dialogService.ShowWarning("Selecciona una orden para recibir.");
            return;
        }

        await _repository.ReceiveAsync(SelectedOrder.IdOrden, _currentUser.IdUsuario);
        _dialogService.ShowInfo($"Orden #{SelectedOrder.IdOrden} recibida y stock actualizado.");
        await LoadAsync();
    }

    private void ValidateQuantity()
    {
        if (!int.TryParse(Quantity, out int quantity) || quantity <= 0)
        {
            SetErrors(nameof(Quantity), new[] { "Ingresa una cantidad valida." });
            return;
        }

        ClearErrors(nameof(Quantity));
    }

    private void ValidatePrice()
    {
        bool parsed = decimal.TryParse(Price, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price)
            || decimal.TryParse(Price, out price);

        if (!parsed || price < 0)
        {
            SetErrors(nameof(Price), new[] { "Ingresa un precio valido." });
            return;
        }

        ClearErrors(nameof(Price));
    }
}






