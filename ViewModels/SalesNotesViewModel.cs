using System.Collections.ObjectModel;
using System.Globalization;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using CamperoDesktop.Views;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace CamperoDesktop.ViewModels;

public class SalesNotesViewModel : ValidatableViewModelBase
{
    private readonly UserSession _currentUser;
    private readonly ISalesNoteRepository _salesRepository;
    private readonly QuotationRepository _quotationRepository;
    private readonly KitRepository _kitRepository;
    private readonly ClientRepository _clientRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly CajaRepository _cajaRepository;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly SaleItemManager _itemManager;
    private WarehouseItem? _selectedWarehouse;
    private ClientOption? _selectedClient;
    private ProductOption? _selectedProduct;
    private SaleDetailDraft? _selectedDetail;
    private string _buyerName = string.Empty;
    private string _buyerCiNit = string.Empty;
    private string _saleDiscount = "0";
    private string _amountReceived = "0";
    private string _quotationCode = string.Empty;
    private string _quotationStatus = "Puedes cargar una cotizacion por codigo.";
    private string _searchText = string.Empty;
    private string _quantity = "1";
    private string _itemDiscount = "0";
    private string _selectedDiscountType = "Monto";
    private string _matchedProductText = "Busca producto o kit por codigo de barras, codigo, ID o nombre.";
    private string _matchedProductStatus = string.Empty;
    private string _subtotalText = "Subtotal: 0.00";
    private string _discountItemsText = "Desc. items: 0.00";
    private string _discountGeneralText = "Desc. venta: 0.00";
    private string _discountTotalText = "Descuento total: 0.00";
    private string _totalText = "Total: 0.00";
    private string _changeText = "Cambio: 0.00";
    private string _historyStatus = string.Empty;
    private string _itemActionText = "Agregar item";
    private bool _isProductDropdownOpen;
    private ProductOption? _matchedProduct;
    private int? _loadedQuotationId;
    private SaleNoteListItem? _selectedHistorySale;
    private DebounceHelper? _searchDebounce;

    public SalesNotesViewModel(UserSession currentUser, ISalesNoteRepository salesRepository, QuotationRepository quotationRepository, KitRepository kitRepository, ClientRepository clientRepository, IInventoryRepository inventoryRepository, CajaRepository cajaRepository, IDialogService dialogService, IServiceProvider serviceProvider)
    {
        _currentUser = currentUser;
        _salesRepository = salesRepository;
        _quotationRepository = quotationRepository;
        _kitRepository = kitRepository;
        _clientRepository = clientRepository;
        _inventoryRepository = inventoryRepository;
        _cajaRepository = cajaRepository;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _itemManager = new SaleItemManager(this, _dialogService, _kitRepository);

        DiscountTypes = new ObservableCollection<string>(new[] { "Monto", "Porcentaje" });
        AddItemCommand = new AsyncRelayCommand(AddCurrentSelectionAsync);
        RemoveItemCommand = new RelayCommand(RemoveSelectedItem);
        CancelItemEditCommand = new RelayCommand(CancelItemEdit);
        LoadQuotationCommand = new AsyncRelayCommand(LoadQuotationAsync);
        EditHistorySaleCommand = new AsyncRelayCommand(EditHistorySaleAsync, CanEditHistorySale);
        CancelHistorySaleCommand = new AsyncRelayCommand(CancelHistorySaleAsync, CanCancelHistorySale);
        RegisterSaleCommand = new AsyncRelayCommand(RegisterSaleAsync, CanRegisterSale);
    }

    public ObservableCollection<WarehouseItem> Warehouses { get; } = new();
    public ObservableCollection<ClientOption> Clients { get; } = new();
    public ObservableCollection<ProductOption> Products { get; } = new();
    public ObservableCollection<SaleDetailDraft> Items { get; } = new();
    public ObservableCollection<SaleNoteListItem> SalesHistory { get; } = new();
    public ObservableCollection<string> DiscountTypes { get; }

    public AsyncRelayCommand AddItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand CancelItemEditCommand { get; }
    public AsyncRelayCommand LoadQuotationCommand { get; }
    public AsyncRelayCommand EditHistorySaleCommand { get; }
    public AsyncRelayCommand CancelHistorySaleCommand { get; }
    public AsyncRelayCommand RegisterSaleCommand { get; }

    public WarehouseItem? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await RefreshProductsAsync(SearchText.Trim()));
                RegisterSaleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ClientOption? SelectedClient { get => _selectedClient; set => SetProperty(ref _selectedClient, value); }

    public ProductOption? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public SaleDetailDraft? SelectedDetail
    {
        get => _selectedDetail;
        set
        {
            if (SetProperty(ref _selectedDetail, value))
            {
                LoadSelectedDetailIntoEditor(value);
            }
        }
    }
    public SaleNoteListItem? SelectedHistorySale
    {
        get => _selectedHistorySale;
        set
        {
            if (SetProperty(ref _selectedHistorySale, value))
            {
                EditHistorySaleCommand.RaiseCanExecuteChanged();
                CancelHistorySaleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string BuyerName
    {
        get => _buyerName;
        set
        {
            if (SetProperty(ref _buyerName, value))
            {
                ValidateBuyerCiNit();
                RegisterSaleCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string BuyerCiNit
    {
        get => _buyerCiNit;
        set
        {
            if (SetProperty(ref _buyerCiNit, value))
            {
                ValidateBuyerCiNit();
                RegisterSaleCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string SaleDiscount
    {
        get => _saleDiscount;
        set
        {
            if (SetProperty(ref _saleDiscount, value))
            {
                ValidateSaleDiscount();
                UpdateTotals();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _searchDebounce?.Cancel();
                _searchDebounce = DebounceHelper.Debounce(() => RefreshProductsAsync(value.Trim()), TimeSpan.FromMilliseconds(300));
            }
        }
    }

    public string Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                ValidateQuantity();
            }
        }
    }
    public string ItemDiscount
    {
        get => _itemDiscount;
        set
        {
            if (SetProperty(ref _itemDiscount, value))
            {
                ValidateItemDiscount();
            }
        }
    }
    public string QuotationCode
    {
        get => _quotationCode;
        set => SetProperty(ref _quotationCode, value);
    }
    public string QuotationStatus
    {
        get => _quotationStatus;
        set => SetProperty(ref _quotationStatus, value);
    }
    public string AmountReceived
    {
        get => _amountReceived;
        set
        {
            if (SetProperty(ref _amountReceived, value))
            {
                ClearErrors(nameof(AmountReceived));
                UpdateTotals();
            }
        }
    }
    public string SelectedDiscountType { get => _selectedDiscountType; set => SetProperty(ref _selectedDiscountType, value); }
    public string MatchedProductText { get => _matchedProductText; set => SetProperty(ref _matchedProductText, value); }
    public string MatchedProductStatus { get => _matchedProductStatus; set => SetProperty(ref _matchedProductStatus, value); }
    public string SubtotalText { get => _subtotalText; set => SetProperty(ref _subtotalText, value); }
    public string DiscountItemsText { get => _discountItemsText; set => SetProperty(ref _discountItemsText, value); }
    public string DiscountGeneralText { get => _discountGeneralText; set => SetProperty(ref _discountGeneralText, value); }
    public string DiscountTotalText { get => _discountTotalText; set => SetProperty(ref _discountTotalText, value); }
    public string TotalText { get => _totalText; set => SetProperty(ref _totalText, value); }
    public string ChangeText { get => _changeText; set => SetProperty(ref _changeText, value); }
    public string HistoryStatus { get => _historyStatus; set => SetProperty(ref _historyStatus, value); }
    public string ItemActionText { get => _itemActionText; set => SetProperty(ref _itemActionText, value); }
    public bool IsProductDropdownOpen { get => _isProductDropdownOpen; set => SetProperty(ref _isProductDropdownOpen, value); }

    public async Task InitializeAsync()
    {
        try
        {
            ReplaceCollection(Clients, await _clientRepository.GetOptionsAsync());
            ReplaceCollection(Warehouses, await _inventoryRepository.GetWarehousesAsync());

            if (Warehouses.Count > 0)
            {
                SelectedWarehouse = Warehouses[0];
            }

            await LoadHistoryAsync();
            ResetDiscountInputs();
            UpdateMatchedProduct(null, string.Empty);
            UpdateTotals();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo cargar el modulo de ventas.\n\nDetalle: {ex.Message}");
        }
    }

    private async Task RefreshProductsAsync(string search)
    {
        try
        {
            int? warehouseId = SelectedWarehouse?.IdAlmacen;
            string criteria = search ?? string.Empty;
            var products = await _inventoryRepository.GetProductsAsync(warehouseId, criteria, includeKits: true);
            ReplaceCollection(Products, products);

            if (Products.Count > 0 && SelectedProduct is not null)
            {
                SelectedProduct = Products.FirstOrDefault(p => p.IsKit == SelectedProduct.IsKit && p.IdProducto == SelectedProduct.IdProducto && p.IdKit == SelectedProduct.IdKit);
            }

            if (Products.Count > 0 && SelectedProduct is null && string.IsNullOrWhiteSpace(criteria))
            {
                SelectedProduct = Products[0];
            }

            ResolveProductSelection(criteria);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron cargar los productos para venta.\n\nDetalle: {ex.Message}");
        }
    }
    private async Task RefreshProductsAsync()
    {
        try
        {
            int? warehouseId = SelectedWarehouse?.IdAlmacen;
            var products = await _inventoryRepository.GetProductsAsync(warehouseId, includeKits: true);
            ReplaceCollection(Products, products);

            if (Products.Count > 0 && SelectedProduct is null)
            {
                SelectedProduct = Products[0];
            }

            ResolveProductSelection(SearchText.Trim());
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron cargar los productos para venta.\n\nDetalle: {ex.Message}");
        }
    }

    private void ResolveProductSelection(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _matchedProduct = null;
            IsProductDropdownOpen = false;
            UpdateMatchedProduct(null, string.Empty);
            return;
        }

        ProductOption? exactBarcode = Products.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.CodigoBarras) && p.CodigoBarras.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactBarcode is not null) { SelectMatchedProduct(exactBarcode, "Coincidencia por codigo de barras."); return; }

        ProductOption? exactCode = Products.FirstOrDefault(p => p.Codigo.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactCode is not null) { SelectMatchedProduct(exactCode, "Coincidencia por codigo interno."); return; }

        ProductOption? exactId = Products.FirstOrDefault(p => !p.IsKit && p.IdProducto.ToString() == query);
        if (exactId is not null) { SelectMatchedProduct(exactId, "Coincidencia por ID del producto."); return; }

        ProductOption? exactName = Products.FirstOrDefault(p => p.Nombre.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactName is not null) { SelectMatchedProduct(exactName, "Coincidencia exacta por nombre."); return; }

        var nameMatches = Products.Where(p => p.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
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
            MatchedProductText = string.IsNullOrWhiteSpace(message) ? "Busca producto o kit por codigo de barras, codigo, ID o nombre." : message;
            MatchedProductStatus = string.Empty;
            return;
        }

        string barcodePart = string.IsNullOrWhiteSpace(product.CodigoBarras) ? "Sin codigo de barras" : $"CB: {product.CodigoBarras}";
        string itemType = product.IsKit ? "KIT" : $"ID {product.IdProducto}";
        string stockLabel = product.IsKit ? "Kits disponibles" : "Stock";
        MatchedProductText = $"{barcodePart} | {product.Codigo} | {itemType} | {product.Nombre}";
        MatchedProductStatus = $"{stockLabel}: {product.StockDisponible} | Precio: {product.PrecioVenta:N2} Bs | {message}";
    }

    private async Task LoadHistoryAsync()
    {
        var sales = await _salesRepository.GetAllAsync();
        ReplaceCollection(SalesHistory, sales);
        HistoryStatus = $"{sales.Count} nota(s)";
    }

    private decimal GeneralDiscount => DecimalParser.ParsePositiveOrDefault(SaleDiscount);
    private decimal ReceivedAmount => DecimalParser.ParsePositiveOrDefault(AmountReceived);

    private void UpdateTotals()
    {
        decimal subtotal = Items.Sum(i => i.BaseSubtotal);
        decimal descuentoItems = Items.Sum(i => i.Descuento);
        decimal descuentoGeneral = GeneralDiscount;
        decimal descuentoTotal = Math.Min(subtotal, descuentoItems + descuentoGeneral);
        decimal total = subtotal - descuentoTotal;
        decimal cambio = Math.Max(ReceivedAmount - total, 0);

        SubtotalText = $"Subtotal: {subtotal:N2}";
        DiscountItemsText = $"Desc. items: {descuentoItems:N2}";
        DiscountGeneralText = $"Desc. venta: {descuentoGeneral:N2}";
        DiscountTotalText = $"Descuento total: {descuentoTotal:N2}";
        TotalText = $"Total: {total:N2}";
        ChangeText = $"Cambio: {cambio:N2}";
        RegisterSaleCommand.RaiseCanExecuteChanged();
    }

    private bool CanRegisterSale() => Items.Count > 0 && SelectedWarehouse is not null && !HasErrors;
    private bool CanEditHistorySale() => UserRoles.IsAdmin(_currentUser.Rol) && SelectedHistorySale is not null;

    private async Task AddCurrentSelectionAsync()
    {
        ResolveProductSelection(SearchText.Trim());
        ValidateQuantity();
        ValidateItemDiscount();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Verifica todos los campos del item antes de agregarlo o cambiarlo.");
            return;
        }

        ProductOption? product = _matchedProduct ?? SelectedProduct;
        if (product is null)
        {
            _dialogService.ShowWarning("Selecciona un producto o kit.");
            return;
        }

        if (!int.TryParse(Quantity, out int cantidad) || cantidad <= 0)
        {
            _dialogService.ShowWarning("Ingresa una cantidad valida.");
            return;
        }

        if (!DecimalParser.TryParsePositive(ItemDiscount, out decimal descuentoValor))
        {
            _dialogService.ShowWarning("Descuento invalido.");
            return;
        }

        decimal baseSubtotal = cantidad * product.PrecioVenta;
        if (SelectedDiscountType == "Porcentaje" && descuentoValor > 100)
        {
            _dialogService.ShowWarning("El descuento porcentual no puede ser mayor a 100.");
            return;
        }

        if (SelectedDiscountType == "Monto" && descuentoValor > baseSubtotal)
        {
            _dialogService.ShowWarning("El descuento no puede ser mayor al subtotal del item.");
            return;
        }

        if (product.IsKit)
        {
            await _itemManager.AddKitSelectionAsync(product, cantidad, descuentoValor, SelectedDetail, SelectedWarehouse);
            return;
        }

        if (cantidad > product.StockDisponible)
        {
            _dialogService.ShowWarning($"Stock insuficiente. Disponible: {product.StockDisponible}.");
            return;
        }

        SaleDetailDraft? editingItem = SelectedDetail;
        var comparisonItems = Items.Where(i => i != editingItem).ToList();
        var existingItem = comparisonItems.FirstOrDefault(i =>
            i.IsKit == product.IsKit &&
            i.IdProducto == product.IdProducto &&
            i.IdKit == product.IdKit &&
            i.TipoDescuento == SelectedDiscountType &&
            i.DescuentoValor == descuentoValor);
        int quantityAlreadyUsed = comparisonItems
            .Where(i => i.IsKit == product.IsKit && i.IdProducto == product.IdProducto && i.IdKit == product.IdKit)
            .Sum(i => i.Cantidad);
        int stockDisponibleRestante = product.StockDisponible - quantityAlreadyUsed;
        if (cantidad > stockDisponibleRestante)
        {
            _dialogService.ShowWarning($"No puedes agregar esa cantidad. Stock restante para vender: {Math.Max(stockDisponibleRestante, 0)}.");
            return;
        }

        if (editingItem is not null)
        {
            if (existingItem is not null)
            {
                existingItem.Cantidad += cantidad;
                Items.Remove(editingItem);
            }
            else
            {
                UpdateDraft(editingItem, product, cantidad, descuentoValor);
            }
        }
        else
        {
            if (existingItem is not null)
            {
                existingItem.Cantidad += cantidad;
            }
            else
            {
                Items.Add(new SaleDetailDraft());
                UpdateDraft(Items[^1], product, cantidad, descuentoValor);
            }
        }

        OnPropertyChanged(nameof(Items));
        ResetEditor();
        UpdateTotals();
    }

    private void RemoveSelectedItem()
    {
        if (SelectedDetail is null)
        {
            return;
        }

        Items.Remove(SelectedDetail);
        ResetEditor();
        UpdateTotals();
    }

    private void CancelItemEdit()
    {
        ResetEditor();
    }

    private async Task RegisterSaleAsync()
    {
        ValidateSaleDiscount();
        ValidateBuyerCiNit();
        ValidateAmountReceived();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos de la venta antes de registrar.");
            return;
        }

        if (Items.Count == 0)
        {
            _dialogService.ShowWarning("Agrega al menos un item a la venta.");
            return;
        }

        if (SelectedWarehouse is null)
        {
            _dialogService.ShowWarning("Selecciona un almacen.");
            return;
        }

        decimal subtotal = Items.Sum(i => i.BaseSubtotal);
        if (GeneralDiscount > subtotal - Items.Sum(i => i.Descuento))
        {
            _dialogService.ShowWarning("El descuento general no puede dejar la venta en negativo.");
            return;
        }

        decimal total = subtotal - Math.Min(subtotal, Items.Sum(i => i.Descuento) + GeneralDiscount);
        if (ReceivedAmount < total)
        {
            _dialogService.ShowWarning("El monto recibido no puede ser menor al total de la venta.");
            return;
        }
        decimal cambio = ReceivedAmount - total;

        int? clienteId = SelectedClient?.IdCliente;
        var itemsSnapshot = Items.Select(item => new SaleReceiptItem
        {
            Codigo = item.Codigo,
            Cantidad = item.Cantidad,
            UnidadMedida = item.UnidadMedida,
            Descripcion = string.IsNullOrWhiteSpace(item.Descripcion) ? item.Producto : item.Descripcion,
            PrecioUnitario = item.PrecioUnitario,
            Descuento = item.Descuento,
            Subtotal = item.Subtotal
        }).ToList();

        string? nombreCliente = SelectedClient?.Nombre;
        if (string.IsNullOrWhiteSpace(nombreCliente)) nombreCliente = BuyerName.Trim();
        string? ciNit = SelectedClient?.CiNit;
        if (string.IsNullOrWhiteSpace(ciNit)) ciNit = BuyerCiNit.Trim();

        SaleCreatedResult result = await _salesRepository.CreateSaleAsync(new SaleCreateRequest
        {
            IdCliente = clienteId,
            NombreComprador = BuyerName.Trim(),
            CiNitComprador = BuyerCiNit.Trim(),
            IdUsuario = _currentUser.IdUsuario,
            IdAlmacen = SelectedWarehouse.IdAlmacen,
            DescuentoAdicional = GeneralDiscount,
            Items = Items.ToList()
        });

        // Registrar el pago en la sesion de caja activa
        SesionCajaInfo? sesionAbierta = await _cajaRepository.GetSesionAbiertaAsync(_currentUser.IdUsuario, SelectedWarehouse.IdAlmacen);
        if (sesionAbierta is null)
        {
            _dialogService.ShowWarning("No hay una sesion de caja abierta. La venta se registro pero no se pudo registrar el pago.");
        }
        else
        {
            // Determinar metodo de pago (por defecto efectivo)
            string metodoPago = "efectivo";
            await _cajaRepository.RegistrarPagoAsync(result.IdNota, sesionAbierta.IdSesion, metodoPago, total, null);
        }

        if (_loadedQuotationId.HasValue)
        {
            await _quotationRepository.MarkAsConvertedAsync(_loadedQuotationId.Value, result.IdNota);
        }

        try
        {
            string receiptPath = SaleReceiptService.GenerateReceiptPdf(new SaleReceiptDocument
            {
                NroNota = result.NroNota,
                Fecha = result.Fecha,
                NombreCliente = string.IsNullOrWhiteSpace(nombreCliente) ? "S/N" : nombreCliente,
                CiNit = string.IsNullOrWhiteSpace(ciNit) ? "S/N" : ciNit,
                CodigoCliente = clienteId?.ToString() ?? "0",
                Subtotal = result.Subtotal,
                Descuento = result.Descuento,
                Total = result.Total,
                MontoRecibido = ReceivedAmount,
                Cambio = cambio,
                MontoGiftCard = 0,
                Items = itemsSnapshot
            });

            SaleReceiptPreviewWindow previewWindow = new(receiptPath, result.NroNota);
            previewWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            previewWindow.ShowDialog();
            _dialogService.ShowInfo($"Venta registrada. Nota: {result.NroNota}\nVista previa lista para impresion.", "Exito");
        }
        catch (Exception ex)
        {
            _dialogService.ShowWarning($"Venta registrada. Nota: {result.NroNota}\nNo se pudo generar o imprimir la nota.\n\nDetalle: {ex.Message}");
        }

        Items.Clear();
        BuyerName = string.Empty;
        BuyerCiNit = string.Empty;
        SaleDiscount = "0";
        AmountReceived = "0";
        QuotationCode = string.Empty;
        QuotationStatus = "Puedes cargar una cotizacion por codigo.";
        _loadedQuotationId = null;
        ResetEditor();
        UpdateTotals();
        await RefreshProductsAsync();
        await LoadHistoryAsync();
    }

    private async Task LoadQuotationAsync()
    {
        if (string.IsNullOrWhiteSpace(QuotationCode))
        {
            _dialogService.ShowWarning("Ingresa el codigo de cotizacion.");
            return;
        }

        QuotationLoadResult? quotation = await _quotationRepository.GetByCodeAsync(QuotationCode.Trim());
        if (quotation is null)
        {
            _dialogService.ShowWarning("No se encontro una cotizacion con ese codigo.");
            QuotationStatus = "Cotizacion no encontrada.";
            return;
        }

        if (quotation.Estado.Trim().Equals("convertida", StringComparison.OrdinalIgnoreCase))
        {
            _dialogService.ShowWarning("Esa cotizacion ya fue convertida a una nota de venta.");
            QuotationStatus = $"La cotizacion {quotation.Codigo} ya fue convertida.";
            return;
        }

        WarehouseItem? warehouse = Warehouses.FirstOrDefault(w => w.IdAlmacen == quotation.IdAlmacen);
        if (warehouse is not null)
        {
            SelectedWarehouse = warehouse;
            await RefreshProductsAsync();
        }

        SelectedClient = quotation.IdCliente.HasValue
            ? Clients.FirstOrDefault(c => c.IdCliente == quotation.IdCliente.Value)
            : null;

        BuyerName = quotation.NombreCliente;
        BuyerCiNit = quotation.CiNitCliente;
        AmountReceived = "0";

        Items.Clear();
        foreach (SaleDetailDraft item in quotation.Items)
        {
            Items.Add(new SaleDetailDraft
            {
                IdProducto = item.IdProducto,
                IdKit = item.IdKit,
                IsKit = item.IsKit,
                Codigo = item.Codigo,
                CodigoBarras = item.CodigoBarras,
                Producto = item.Producto,
                Descripcion = item.Descripcion,
                UnidadMedida = item.UnidadMedida,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                TipoDescuento = item.TipoDescuento,
                DescuentoValor = item.DescuentoValor
            });
        }

        decimal itemDiscountTotal = Items.Sum(i => i.Descuento);
        decimal generalDiscount = Math.Max(quotation.Descuento - itemDiscountTotal, 0);
        SaleDiscount = generalDiscount.ToString("0.##", CultureInfo.InvariantCulture);

        _loadedQuotationId = quotation.IdCotizacion;
        QuotationCode = quotation.Codigo;
        QuotationStatus = $"Cotizacion {quotation.Codigo} cargada en venta. Puedes ajustarla antes de registrar.";
        ResetEditor();
        UpdateTotals();
    }

    private async Task EditHistorySaleAsync()
    {
        if (!CanEditHistorySale())
        {
            return;
        }

        SaleNoteEditViewModel viewModel = ActivatorUtilities.CreateInstance<SaleNoteEditViewModel>(_serviceProvider, _currentUser.IdUsuario);
        await viewModel.InitializeAsync(SelectedHistorySale!.IdNota);

        SaleNoteEditWindow window = new(viewModel)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };

        bool? result = window.ShowDialog();
        if (result == true)
        {
            await RefreshProductsAsync();
            await LoadHistoryAsync();
        }
    }

    private bool CanCancelHistorySale() => UserRoles.IsAdmin(_currentUser.Rol) && SelectedHistorySale is not null && SelectedHistorySale.Estado.Trim().Equals("completada", StringComparison.OrdinalIgnoreCase);

    private async Task CancelHistorySaleAsync()
    {
        if (!CanCancelHistorySale() || SelectedHistorySale is null)
        {
            return;
        }

        bool? confirm = _dialogService.ShowConfirmation("¿Estas seguro de anular esta nota de venta? Esta accion restaurara el inventario y no puede deshacerse.");
        if (confirm != true)
        {
            return;
        }

        string? motivo = _dialogService.ShowInputDialog("Motivo de anulacion", "Ingresa el motivo de la anulacion:");
        if (string.IsNullOrWhiteSpace(motivo))
        {
            _dialogService.ShowWarning("La anulacion requiere un motivo.");
            return;
        }

        try
        {
            await _salesRepository.AnularNotaAsync(SelectedHistorySale.IdNota, _currentUser.IdUsuario, motivo.Trim());
            _dialogService.ShowInfo($"Nota {SelectedHistorySale.NroNota} anulada correctamente.");
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowWarning($"No se pudo anular la nota: {ex.Message}");
        }
    }

    private void ResetDiscountInputs()
    {
        ItemDiscount = "0";
        SelectedDiscountType = "Monto";
    }

    private void LoadSelectedDetailIntoEditor(SaleDetailDraft? detail)
    {
        if (detail is null)
        {
            ItemActionText = "Agregar item";
            return;
        }

        ProductOption? product = detail.IsKit
            ? Products.FirstOrDefault(p => p.IsKit && p.IdKit == detail.IdKit)
            : Products.FirstOrDefault(p => !p.IsKit && p.IdProducto == detail.IdProducto);
        _matchedProduct = product;
        SelectedProduct = product;
        SearchText = !string.IsNullOrWhiteSpace(detail.CodigoBarras) ? detail.CodigoBarras : detail.Codigo;
        Quantity = detail.Cantidad.ToString(CultureInfo.InvariantCulture);
        SelectedDiscountType = detail.TipoDescuento;
        ItemDiscount = detail.DescuentoValor.ToString("0.##", CultureInfo.InvariantCulture);
        UpdateMatchedProduct(product, "Editando item seleccionado.");
        ItemActionText = "Guardar cambios";
    }

    private void ResetEditor()
    {
        SelectedDetail = null;
        _matchedProduct = null;
        SelectedProduct = null;
        SearchText = string.Empty;
        Quantity = "1";
        ResetDiscountInputs();
        UpdateMatchedProduct(null, "Busca el siguiente producto.");
        ItemActionText = "Agregar item";
    }

    internal void ResetEditorPublic() => ResetEditor();
    internal void UpdateTotalsPublic() => UpdateTotals();

    private void UpdateDraft(SaleDetailDraft draft, ProductOption product, int cantidad, decimal descuentoValor)
    {
        draft.IdProducto = product.IdProducto;
        draft.IdKit = product.IdKit;
        draft.IsKit = product.IsKit;
        draft.Codigo = product.Codigo;
        draft.CodigoBarras = product.CodigoBarras;
        draft.Producto = product.Nombre;
        draft.Descripcion = string.IsNullOrWhiteSpace(product.Descripcion) ? product.Nombre : product.Descripcion;
        draft.UnidadMedida = product.UnidadMedida;
        draft.Cantidad = cantidad;
        draft.PrecioUnitario = product.PrecioVenta;
        draft.TipoDescuento = SelectedDiscountType;
        draft.DescuentoValor = descuentoValor;
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

    private void ValidateItemDiscount()
    {
        if (!DecimalParser.TryParsePositive(ItemDiscount, out decimal itemDiscount))
        {
            SetErrors(nameof(ItemDiscount), new[] { "Ingresa un descuento valido." });
            return;
        }

        if (SelectedDiscountType == "Porcentaje" && itemDiscount > 100)
        {
            SetErrors(nameof(ItemDiscount), new[] { "El descuento porcentual no puede ser mayor a 100." });
            return;
        }

        ClearErrors(nameof(ItemDiscount));
    }

    private void ValidateSaleDiscount()
    {
        if (!DecimalParser.TryParsePositive(SaleDiscount, out _))
        {
            SetErrors(nameof(SaleDiscount), new[] { "Ingresa un descuento general valido." });
            return;
        }

        ClearErrors(nameof(SaleDiscount));
    }

    private void ValidateAmountReceived()
    {
        if (!DecimalParser.TryParsePositive(AmountReceived, out _))
        {
            SetErrors(nameof(AmountReceived), new[] { "Ingresa un monto recibido valido." });
            return;
        }

        decimal subtotal = Items.Sum(i => i.BaseSubtotal);
        decimal total = subtotal - Math.Min(subtotal, Items.Sum(i => i.Descuento) + GeneralDiscount);
        if (Items.Count > 0 && ReceivedAmount < total)
        {
            SetErrors(nameof(AmountReceived), new[] { "El monto recibido debe cubrir el total de la venta." });
            return;
        }

        ClearErrors(nameof(AmountReceived));
    }

    private void ValidateBuyerCiNit()
    {
        if (!string.IsNullOrWhiteSpace(BuyerName) && string.IsNullOrWhiteSpace(BuyerCiNit) && SelectedClient is null)
        {
            SetErrors(nameof(BuyerCiNit), new[] { "Ingresa CI/NIT para comprador ocasional." });
            return;
        }

        ClearErrors(nameof(BuyerCiNit));
    }
}

public class SaleItemManager
{
    private readonly SalesNotesViewModel _parent;
    private readonly IDialogService _dialogService;
    private readonly KitRepository _kitRepository;

    public SaleItemManager(SalesNotesViewModel parent, IDialogService dialogService, KitRepository kitRepository)
    {
        _parent = parent;
        _dialogService = dialogService;
        _kitRepository = kitRepository;
    }

    public async Task AddKitSelectionAsync(ProductOption kit, int kitCantidad, decimal descuentoValor, SaleDetailDraft? editingItem, WarehouseItem? selectedWarehouse)
    {
        if (selectedWarehouse is null)
        {
            _dialogService.ShowWarning("Selecciona un almacen.");
            return;
        }

        if (!kit.IdKit.HasValue)
        {
            _dialogService.ShowWarning("El kit seleccionado no tiene un identificador valido.");
            return;
        }

        List<KitComponentDraft> components = await _kitRepository.GetKitComponentsAsync(kit.IdKit.Value, selectedWarehouse.IdAlmacen);
        components = components.Where(component => component.Cantidad > 0).ToList();
        if (components.Count == 0)
        {
            _dialogService.ShowWarning("El kit no tiene componentes configurados.");
            return;
        }

        var comparisonItems = _parent.Items.Where(item => item != editingItem).ToList();
        foreach (KitComponentDraft component in components)
        {
            int requiredQuantity = component.Cantidad * kitCantidad;
            int quantityAlreadyUsed = comparisonItems
                .Where(item => !item.IsKit && item.IdProducto == component.IdProducto)
                .Sum(item => item.Cantidad);
            int remainingStock = component.StockDisponible - quantityAlreadyUsed;
            if (requiredQuantity > remainingStock)
            {
                _dialogService.ShowWarning($"No hay stock suficiente para desglosar el kit. Componente limitante: {component.Nombre}. Disponible restante: {Math.Max(remainingStock, 0)}.");
                return;
            }
        }

        if (editingItem is not null)
        {
            _parent.Items.Remove(editingItem);
        }

        decimal targetBaseTotal = kit.PrecioVenta * kitCantidad;
        decimal referenceBaseTotal = components.Sum(component => component.PrecioVenta * component.Cantidad * kitCantidad);
        decimal remainingBase = targetBaseTotal;
        decimal remainingDiscount = _parent.SelectedDiscountType == "Monto" ? descuentoValor : 0m;

        for (int index = 0; index < components.Count; index++)
        {
            KitComponentDraft component = components[index];
            int lineQuantity = component.Cantidad * kitCantidad;
            decimal referenceLineBase = component.PrecioVenta * lineQuantity;
            decimal lineBase = index == components.Count - 1
                ? remainingBase
                : referenceBaseTotal <= 0
                    ? 0
                    : Math.Round(targetBaseTotal * referenceLineBase / referenceBaseTotal, 2, MidpointRounding.AwayFromZero);
            remainingBase -= lineBase;

            decimal lineUnitPrice = lineQuantity <= 0 ? 0 : lineBase / lineQuantity;
            string lineDiscountType = _parent.SelectedDiscountType;
            decimal lineDiscountValue;

            if (_parent.SelectedDiscountType == "Porcentaje")
            {
                lineDiscountValue = descuentoValor;
            }
            else
            {
                lineDiscountValue = index == components.Count - 1
                    ? remainingDiscount
                    : targetBaseTotal <= 0
                        ? 0
                        : Math.Round(descuentoValor * lineBase / targetBaseTotal, 2, MidpointRounding.AwayFromZero);
                lineDiscountValue = Math.Min(lineDiscountValue, lineBase);
                remainingDiscount -= lineDiscountValue;
            }

            AddOrMergeExpandedComponentDraft(component, kit, lineQuantity, lineUnitPrice, lineDiscountType, lineDiscountValue);
        }

        _parent.OnPropertyChangedPublic(nameof(_parent.Items));
        _parent.ResetEditorPublic();
        _parent.UpdateTotalsPublic();
    }

    private void AddOrMergeExpandedComponentDraft(KitComponentDraft component, ProductOption kit, int quantity, decimal unitPrice, string discountType, decimal discountValue)
    {
        SaleDetailDraft? existingItem = _parent.Items.FirstOrDefault(item =>
            !item.IsKit &&
            item.IdProducto == component.IdProducto &&
            item.TipoDescuento == discountType &&
            item.DescuentoValor == discountValue &&
            item.PrecioUnitario == unitPrice);

        if (existingItem is not null)
        {
            existingItem.Cantidad += quantity;
            return;
        }

        SaleDetailDraft draft = new();
        draft.IdProducto = component.IdProducto;
        draft.IdKit = null;
        draft.IsKit = false;
        draft.Codigo = component.Codigo;
        draft.CodigoBarras = string.Empty;
        draft.Producto = component.Nombre;
        draft.Descripcion = $"{component.Nombre} | Kit: {kit.Nombre}";
        draft.UnidadMedida = component.UnidadMedida;
        draft.Cantidad = quantity;
        draft.PrecioUnitario = unitPrice;
        draft.TipoDescuento = discountType;
        draft.DescuentoValor = discountValue;
        _parent.Items.Add(draft);
    }
}
