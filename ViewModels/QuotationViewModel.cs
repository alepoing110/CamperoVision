using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class QuotationViewModel : ValidatableViewModelBase
{
    private readonly UserSession _currentUser;
    private readonly QuotationRepository _quotationRepository;
    private readonly KitRepository _kitRepository;
    private readonly ClientRepository _clientRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IDialogService _dialogService;
    private readonly QuotationItemManager _itemManager;
    private WarehouseItem? _selectedWarehouse;
    private ClientOption? _selectedClient;
    private ProductOption? _selectedProduct;
    private SaleDetailDraft? _selectedDetail;
    private string _buyerName = string.Empty;
    private string _buyerCiNit = string.Empty;
    private string _quotationDiscount = "0";
    private string _searchText = string.Empty;
    private string _quantity = "1";
    private string _itemDiscount = "0";
    private string _selectedDiscountType = "Monto";
    private string _observations = "Cotizacion valida por 15 dias.";
    private string _matchedProductText = "Busca producto o kit por codigo de barras, codigo, ID o nombre.";
    private string _matchedProductStatus = string.Empty;
    private string _subtotalText = "Subtotal: 0.00";
    private string _discountItemsText = "Desc. items: 0.00";
    private string _discountGeneralText = "Desc. cotizacion: 0.00";
    private string _discountTotalText = "Descuento total: 0.00";
    private string _totalText = "Total: 0.00";
    private string _itemActionText = "Agregar item";
    private bool _isProductDropdownOpen;
    private string _quotationCodeText = "Codigo pendiente";
    private ProductOption? _matchedProduct;

    public QuotationViewModel(UserSession currentUser, QuotationRepository quotationRepository, KitRepository kitRepository, ClientRepository clientRepository, IInventoryRepository inventoryRepository, IDialogService dialogService)
    {
        _currentUser = currentUser;
        _quotationRepository = quotationRepository;
        _kitRepository = kitRepository;
        _clientRepository = clientRepository;
        _inventoryRepository = inventoryRepository;
        _dialogService = dialogService;
        _itemManager = new QuotationItemManager(this, _dialogService, _kitRepository);

        DiscountTypes = new ObservableCollection<string>(new[] { "Monto", "Porcentaje" });
        AddItemCommand = new AsyncRelayCommand(_itemManager.AddCurrentSelectionAsync);
        RemoveItemCommand = new RelayCommand(_itemManager.RemoveSelectedItem);
        CancelItemEditCommand = new RelayCommand(CancelItemEdit);
        GenerateQuotationCommand = new AsyncRelayCommand(GenerateQuotationAsync, CanGenerateQuotation);
        ClearQuotationCommand = new RelayCommand(ClearQuotation);
    }

    public ObservableCollection<WarehouseItem> Warehouses { get; } = new();
    public ObservableCollection<ClientOption> Clients { get; } = new();
    public ObservableCollection<ProductOption> Products { get; } = new();
    public ObservableCollection<SaleDetailDraft> Items { get; } = new();
    public ObservableCollection<string> DiscountTypes { get; }

    public AsyncRelayCommand AddItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand CancelItemEditCommand { get; }
    public RelayCommand ClearQuotationCommand { get; }
    public AsyncRelayCommand GenerateQuotationCommand { get; }

    public WarehouseItem? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await RefreshProductsAsync(SearchText.Trim()));
                GenerateQuotationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ClientOption? SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (SetProperty(ref _selectedClient, value))
            {
                if (value is not null)
                {
                    BuyerName = value.Nombre;
                    BuyerCiNit = value.CiNit;
                }

                GenerateQuotationCommand.RaiseCanExecuteChanged();
            }
        }
    }

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

    public string BuyerName
    {
        get => _buyerName;
        set
        {
            if (SetProperty(ref _buyerName, value))
            {
                ValidateBuyerCiNit();
                GenerateQuotationCommand.RaiseCanExecuteChanged();
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
                GenerateQuotationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string QuotationDiscount
    {
        get => _quotationDiscount;
        set
        {
            if (SetProperty(ref _quotationDiscount, value))
            {
                ValidateQuotationDiscount();
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
                AsyncHelper.FireAndForgetOnUi(async () => await RefreshProductsAsync(value.Trim()));
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

    public string SelectedDiscountType
    {
        get => _selectedDiscountType;
        set
        {
            if (SetProperty(ref _selectedDiscountType, value))
            {
                ValidateItemDiscount();
            }
        }
    }

    public string Observations { get => _observations; set => SetProperty(ref _observations, value); }
    public string MatchedProductText { get => _matchedProductText; set => SetProperty(ref _matchedProductText, value); }
    public string MatchedProductStatus { get => _matchedProductStatus; set => SetProperty(ref _matchedProductStatus, value); }
    public string SubtotalText { get => _subtotalText; set => SetProperty(ref _subtotalText, value); }
    public string DiscountItemsText { get => _discountItemsText; set => SetProperty(ref _discountItemsText, value); }
    public string DiscountGeneralText { get => _discountGeneralText; set => SetProperty(ref _discountGeneralText, value); }
    public string DiscountTotalText { get => _discountTotalText; set => SetProperty(ref _discountTotalText, value); }
    public string TotalText { get => _totalText; set => SetProperty(ref _totalText, value); }
    public string ItemActionText { get => _itemActionText; set => SetProperty(ref _itemActionText, value); }
    public bool IsProductDropdownOpen { get => _isProductDropdownOpen; set => SetProperty(ref _isProductDropdownOpen, value); }
    public string QuotationCodeText { get => _quotationCodeText; set => SetProperty(ref _quotationCodeText, value); }

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

            ResetDiscountInputs();
            UpdateMatchedProduct(null, string.Empty);
            UpdateTotals();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo cargar el modulo de cotizaciones.\n\nDetalle: {ex.Message}");
        }
    }

    private async Task RefreshProductsAsync(string search)
    {
        try
        {
            int? warehouseId = SelectedWarehouse?.IdAlmacen;
            string criteria = search ?? string.Empty;
            List<ProductOption> products = await _inventoryRepository.GetProductsAsync(warehouseId, criteria, includeKits: true);
            ReplaceCollection(Products, products);

            if (Products.Count > 0 && SelectedProduct is not null)
            {
                SelectedProduct = Products.FirstOrDefault(p => p.IsKit == SelectedProduct.IsKit && p.IdProducto == SelectedProduct.IdProducto && p.IdKit == SelectedProduct.IdKit);
            }

            ResolveProductSelection(criteria);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron cargar los productos para cotizacion.\n\nDetalle: {ex.Message}");
        }
    }
    private async Task RefreshProductsAsync()
    {
        try
        {
            int? warehouseId = SelectedWarehouse?.IdAlmacen;
            List<ProductOption> products = await _inventoryRepository.GetProductsAsync(warehouseId, includeKits: true);
            ReplaceCollection(Products, products);

            ResolveProductSelection(SearchText.Trim());
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron cargar los productos para cotizacion.\n\nDetalle: {ex.Message}");
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
            MatchedProductText = string.IsNullOrWhiteSpace(message) ? "Busca producto o kit por codigo de barras, codigo, ID o nombre." : message;
            MatchedProductStatus = string.Empty;
            return;
        }

        string barcodePart = string.IsNullOrWhiteSpace(product.CodigoBarras) ? "Sin codigo de barras" : $"CB: {product.CodigoBarras}";
        string itemType = product.IsKit ? "KIT" : $"ID {product.IdProducto}";
        string stockLabel = product.IsKit ? "Kits disponibles" : "Stock referencial";
        MatchedProductText = $"{barcodePart} | {product.Codigo} | {itemType} | {product.Nombre}";
        MatchedProductStatus = $"{stockLabel}: {product.StockDisponible} | Precio: {product.PrecioVenta:N2} Bs | {message}";
    }

    private bool CanGenerateQuotation() => Items.Count > 0 && SelectedWarehouse is not null && !HasErrors;

    private void CancelItemEdit()
    {
        ResetEditor();
    }

    private async Task GenerateQuotationAsync()
    {
        ValidateQuotationDiscount();
        ValidateBuyerCiNit();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos antes de generar la cotizacion.");
            return;
        }

        if (Items.Count == 0)
        {
            _dialogService.ShowWarning("Agrega al menos un item a la cotizacion.");
            return;
        }

        decimal subtotal = Items.Sum(i => i.BaseSubtotal);
        decimal itemDiscounts = Items.Sum(i => i.Descuento);
        decimal generalDiscount = GeneralDiscount;
        decimal totalDiscount = Math.Min(subtotal, itemDiscounts + generalDiscount);
        decimal total = subtotal - totalDiscount;

        if (total < 0)
        {
            _dialogService.ShowWarning("La cotizacion no puede quedar en negativo.");
            return;
        }

        string clientName = SelectedClient?.Nombre ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientName))
        {
            clientName = string.IsNullOrWhiteSpace(BuyerName) ? "Cliente ocasional" : BuyerName.Trim();
        }

        string ciNit = SelectedClient?.CiNit ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ciNit))
        {
            ciNit = string.IsNullOrWhiteSpace(BuyerCiNit) ? "S/N" : BuyerCiNit.Trim();
        }

        QuotationDocument document = new()
        {
            Numero = string.Empty,
            Fecha = DateTime.Now,
            Cliente = clientName,
            CiNit = ciNit,
            Vendedor = _currentUser.Nombre,
            Subtotal = subtotal,
            Descuento = totalDiscount,
            Total = total,
            Observaciones = Observations.Trim(),
            Items = Items.Select(item => new SaleReceiptItem
            {
                Codigo = item.Codigo,
                Cantidad = item.Cantidad,
                UnidadMedida = item.UnidadMedida,
                Descripcion = string.IsNullOrWhiteSpace(item.Descripcion) ? item.Producto : item.Descripcion,
                PrecioUnitario = item.PrecioUnitario,
                Descuento = item.Descuento,
                Subtotal = item.Subtotal
            }).ToList()
        };

        string quotationCode = await _quotationRepository.CreateAsync(new QuotationSaveRequest
        {
            Codigo = string.Empty,
            IdCliente = SelectedClient?.IdCliente,
            NombreCliente = clientName,
            CiNitCliente = ciNit,
            IdUsuario = _currentUser.IdUsuario,
            IdAlmacen = SelectedWarehouse!.IdAlmacen,
            Fecha = document.Fecha,
            Subtotal = subtotal,
            Descuento = totalDiscount,
            Total = total,
            Observaciones = document.Observaciones,
            Items = Items.Select(item => new SaleDetailDraft
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
            }).ToList()
        });

        document.Numero = quotationCode;

        string pdfPath = await Task.Run(() => QuotationPrintService.GenerateQuotationPdf(document));
        QuotationPrintService.OpenPdf(pdfPath);
        _dialogService.ShowInfo($"Cotizacion generada correctamente.\nCodigo: {quotationCode}\nArchivo: {Path.GetFileName(pdfPath)}", "Cotizacion");
        ClearQuotation();
        QuotationCodeText = quotationCode;
    }

    private decimal GeneralDiscount => ParsePositiveDecimal(QuotationDiscount);

    private static decimal ParsePositiveDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed) || decimal.TryParse(value, out parsed)
            ? Math.Max(parsed, 0)
            : 0;
    }

    private void UpdateTotals()
    {
        decimal subtotal = Items.Sum(i => i.BaseSubtotal);
        decimal discountItems = Items.Sum(i => i.Descuento);
        decimal discountGeneral = GeneralDiscount;
        decimal discountTotal = Math.Min(subtotal, discountItems + discountGeneral);
        decimal total = subtotal - discountTotal;

        SubtotalText = $"Subtotal: {subtotal:N2}";
        DiscountItemsText = $"Desc. items: {discountItems:N2}";
        DiscountGeneralText = $"Desc. cotizacion: {discountGeneral:N2}";
        DiscountTotalText = $"Descuento total: {discountTotal:N2}";
        TotalText = $"Total: {total:N2}";
        GenerateQuotationCommand.RaiseCanExecuteChanged();
    }

    private void ClearQuotation()
    {
        ClearAllErrors();
        Items.Clear();
        SelectedClient = null;
        BuyerName = string.Empty;
        BuyerCiNit = string.Empty;
        QuotationDiscount = "0";
        Observations = "Cotizacion valida por 15 dias.";
        ResetEditor();
        UpdateTotals();
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

    private void UpdateDraft(SaleDetailDraft draft, ProductOption product, int quantity, decimal discountValue)
    {
        draft.IdProducto = product.IdProducto;
        draft.IdKit = product.IdKit;
        draft.IsKit = product.IsKit;
        draft.Codigo = product.Codigo;
        draft.CodigoBarras = product.CodigoBarras;
        draft.Producto = product.Nombre;
        draft.Descripcion = string.IsNullOrWhiteSpace(product.Descripcion) ? product.Nombre : product.Descripcion;
        draft.UnidadMedida = product.UnidadMedida;
        draft.Cantidad = quantity;
        draft.PrecioUnitario = product.PrecioVenta;
        draft.TipoDescuento = SelectedDiscountType;
        draft.DescuentoValor = discountValue;
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
        bool parsed = decimal.TryParse(ItemDiscount, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal itemDiscount)
            || decimal.TryParse(ItemDiscount, out itemDiscount);

        if (!parsed || itemDiscount < 0)
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

    private void ValidateQuotationDiscount()
    {
        bool parsed = decimal.TryParse(QuotationDiscount, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal quotationDiscount)
            || decimal.TryParse(QuotationDiscount, out quotationDiscount);

        if (!parsed || quotationDiscount < 0)
        {
            SetErrors(nameof(QuotationDiscount), new[] { "Ingresa un descuento general valido." });
            return;
        }

        ClearErrors(nameof(QuotationDiscount));
    }

    private void ValidateBuyerCiNit()
    {
        if (!string.IsNullOrWhiteSpace(BuyerName) && string.IsNullOrWhiteSpace(BuyerCiNit) && SelectedClient is null)
        {
            SetErrors(nameof(BuyerCiNit), new[] { "Ingresa CI/NIT para cliente ocasional." });
            return;
        }

        ClearErrors(nameof(BuyerCiNit));
    }

    internal void ResolveProductSelectionPublic(string query) => ResolveProductSelection(query);
    internal void ValidateQuantityPublic() => ValidateQuantity();
    internal void ValidateItemDiscountPublic() => ValidateItemDiscount();
    internal void ResetEditorPublic() => ResetEditor();
    internal void UpdateTotalsPublic() => UpdateTotals();
    internal ProductOption? MatchedProductPublic => _matchedProduct;
    internal ProductOption? SelectedProductPublic => SelectedProduct;
    internal SaleDetailDraft? SelectedDetailPublic => SelectedDetail;
    internal WarehouseItem? SelectedWarehousePublic => SelectedWarehouse;
}


















