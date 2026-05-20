using System.Collections.ObjectModel;
using System.Globalization;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class SaleNoteEditViewModel : ValidatableViewModelBase
{
    private readonly ISalesNoteRepository _repository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IDialogService _dialogService;
    private readonly int _editorUserId;
    private int _idNota;
    private int _warehouseId;
    private string _nroNota = string.Empty;
    private string _buyerName = string.Empty;
    private string _buyerCiNit = string.Empty;
    private string _generalDiscount = "0";
    private string _searchText = string.Empty;
    private string _quantity = "1";
    private string _price = "0";
    private string _discount = "0";
    private string _statusText = string.Empty;
    private string _itemActionText = "Agregar item";
    private string _matchedProductText = "Busca por codigo de barras, codigo o nombre.";
    private bool _isProductDropdownOpen;
    private SaleNoteEditDetailModel? _selectedItem;
    private ProductOption? _selectedProduct;
    private ProductOption? _matchedProduct;

    public SaleNoteEditViewModel(int editorUserId, ISalesNoteRepository repository, IInventoryRepository inventoryRepository, IDialogService dialogService)
    {
        _editorUserId = editorUserId;
        _repository = repository;
        _inventoryRepository = inventoryRepository;
        _dialogService = dialogService;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RemoveItemCommand = new RelayCommand(RemoveSelectedItem);
        AddOrUpdateItemCommand = new RelayCommand(AddOrUpdateItem);
        CancelItemEditCommand = new RelayCommand(CancelItemEdit);
    }

    public event EventHandler<bool>? CloseRequested;

    public ObservableCollection<SaleNoteEditDetailModel> Items { get; } = new();
    public ObservableCollection<ProductOption> Products { get; } = new();
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand AddOrUpdateItemCommand { get; }
    public RelayCommand CancelItemEditCommand { get; }

    public string NroNota { get => _nroNota; set => SetProperty(ref _nroNota, value); }
    public string BuyerName { get => _buyerName; set => SetProperty(ref _buyerName, value); }
    public string BuyerCiNit { get => _buyerCiNit; set => SetProperty(ref _buyerCiNit, value); }
    public string GeneralDiscount
    {
        get => _generalDiscount;
        set
        {
            if (SetProperty(ref _generalDiscount, value))
            {
                ValidateGeneralDiscount();
                UpdateStatus();
            }
        }
    }

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
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
        set => SetProperty(ref _quantity, value);
    }
    public string Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }
    public string Discount
    {
        get => _discount;
        set => SetProperty(ref _discount, value);
    }
    public string ItemActionText { get => _itemActionText; set => SetProperty(ref _itemActionText, value); }
    public string MatchedProductText { get => _matchedProductText; set => SetProperty(ref _matchedProductText, value); }
    public bool IsProductDropdownOpen { get => _isProductDropdownOpen; set => SetProperty(ref _isProductDropdownOpen, value); }
    public ProductOption? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value) && value is not null && SelectedItem is null)
            {
                _matchedProduct = value;
                Price = value.PrecioVenta.ToString("0.##", CultureInfo.InvariantCulture);
                MatchedProductText = $"{value.Codigo} | {value.Nombre} | {(value.IsKit ? "Kits disponibles" : "Stock")}: {value.StockDisponible}";
            }
        }
    }
    public SaleNoteEditDetailModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                LoadSelectedItemIntoEditor(value);
            }
        }
    }

    public async Task InitializeAsync(int idNota)
    {
        SaleNoteEditModel model = await _repository.GetEditModelAsync(idNota);
        _idNota = model.IdNota;
        _warehouseId = model.IdAlmacen;
        NroNota = model.NroNota;
        BuyerName = model.BuyerName;
        BuyerCiNit = model.BuyerCiNit;
        GeneralDiscount = model.GeneralDiscount.ToString("0.##", CultureInfo.InvariantCulture);
        ReplaceCollection(Products, await _inventoryRepository.GetProductsAsync(_warehouseId, includeKits: true));
        Items.Clear();
        foreach (SaleNoteEditDetailModel item in model.Items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
        }
        ResetItemEditor();
        UpdateStatus();
    }

    private async Task RefreshProductsAsync(string? search = null)
    {
        string criteria = search ?? SearchText.Trim();
        ReplaceCollection(Products, await _inventoryRepository.GetProductsAsync(_warehouseId, criteria, includeKits: true));

        ResolveProductSelection(criteria);
    }
    private async Task SaveAsync()
    {
        ValidateGeneralDiscount();
        ValidateItems();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos de la nota antes de guardar.");
            return;
        }

        await _repository.UpdateAsync(new SaleNoteEditModel
        {
            IdNota = _idNota,
            EditorUserId = _editorUserId,
            NroNota = NroNota,
            BuyerName = BuyerName.Trim(),
            BuyerCiNit = BuyerCiNit.Trim(),
            GeneralDiscount = ParsePositiveDecimal(GeneralDiscount),
            Items = Items.Select(item => new SaleNoteEditDetailModel
            {
                IdProducto = item.IdProducto,
                IdKit = item.IdKit,
                IsKit = item.IsKit,
                Codigo = item.Codigo,
                Producto = item.Producto,
                Descripcion = item.Descripcion,
                UnidadMedida = item.UnidadMedida,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                DescuentoMonto = item.DescuentoMonto
            }).ToList()
        });

        _dialogService.ShowInfo("Nota de venta actualizada correctamente.", "Ventas");
        CloseRequested?.Invoke(this, true);
    }

    private void RemoveSelectedItem()
    {
        if (SelectedItem is null)
        {
            return;
        }

        SelectedItem.PropertyChanged -= OnItemPropertyChanged;
        Items.Remove(SelectedItem);
        ResetItemEditor();
        UpdateStatus();
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateStatus();
    }

    private void AddOrUpdateItem()
    {
        ProductOption? product = _matchedProduct ?? SelectedProduct;
        if (product is null)
        {
            _dialogService.ShowWarning("Selecciona un producto para el detalle.");
            return;
        }

        if (!int.TryParse(Quantity, out int quantity) || quantity <= 0)
        {
            _dialogService.ShowWarning("Ingresa una cantidad valida.");
            return;
        }

        if (!decimal.TryParse(Price, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price)
            && !decimal.TryParse(Price, out price))
        {
            _dialogService.ShowWarning("Ingresa un precio valido.");
            return;
        }

        if (!decimal.TryParse(Discount, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal discount)
            && !decimal.TryParse(Discount, out discount))
        {
            _dialogService.ShowWarning("Ingresa un descuento valido.");
            return;
        }

        if (price < 0 || discount < 0)
        {
            _dialogService.ShowWarning("Precio y descuento no pueden ser negativos.");
            return;
        }

        SaleNoteEditDetailModel? editingItem = SelectedItem;
        if (editingItem is not null)
        {
            editingItem.IdProducto = product.IdProducto;
            editingItem.IdKit = product.IdKit;
            editingItem.IsKit = product.IsKit;
            editingItem.Codigo = product.Codigo;
            editingItem.Producto = product.Nombre;
            editingItem.Descripcion = string.IsNullOrWhiteSpace(product.Descripcion) ? product.Nombre : product.Descripcion;
            editingItem.UnidadMedida = product.UnidadMedida;
            editingItem.Cantidad = quantity;
            editingItem.PrecioUnitario = price;
            editingItem.DescuentoMonto = discount;
        }
        else
        {
            SaleNoteEditDetailModel item = new()
            {
                IdProducto = product.IdProducto,
                IdKit = product.IdKit,
                IsKit = product.IsKit,
                Codigo = product.Codigo,
                Producto = product.Nombre,
                Descripcion = string.IsNullOrWhiteSpace(product.Descripcion) ? product.Nombre : product.Descripcion,
                UnidadMedida = product.UnidadMedida,
                Cantidad = quantity,
                PrecioUnitario = price,
                DescuentoMonto = discount
            };
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
        }

        ResetItemEditor();
        UpdateStatus();
    }

    private void CancelItemEdit()
    {
        ResetItemEditor();
    }

    private void ValidateItems()
    {
        if (Items.Count == 0)
        {
            SetErrors(nameof(Items), new[] { "La nota debe tener al menos un producto." });
            return;
        }

        bool hasInvalid = Items.Any(item => item.Cantidad <= 0 || item.PrecioUnitario < 0 || item.DescuentoMonto < 0 || item.DescuentoMonto > item.BaseSubtotal);
        if (hasInvalid)
        {
            SetErrors(nameof(Items), new[] { "Verifica cantidades, precios y descuentos del detalle." });
            return;
        }

        ClearErrors(nameof(Items));
    }

    private void ValidateGeneralDiscount()
    {
        if (!decimal.TryParse(GeneralDiscount, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value)
            && !decimal.TryParse(GeneralDiscount, out value))
        {
            SetErrors(nameof(GeneralDiscount), new[] { "Ingresa un descuento general valido." });
            return;
        }

        if (value < 0)
        {
            SetErrors(nameof(GeneralDiscount), new[] { "El descuento general no puede ser negativo." });
            return;
        }

        ClearErrors(nameof(GeneralDiscount));
    }

    private void UpdateStatus()
    {
        decimal subtotal = Items.Sum(i => i.BaseSubtotal);
        decimal descuentoItems = Items.Sum(i => i.DescuentoAplicado);
        decimal descuentoGeneral = ParsePositiveDecimal(GeneralDiscount);
        decimal descuentoTotal = Math.Min(subtotal, descuentoItems + descuentoGeneral);
        decimal total = subtotal - descuentoTotal;
        StatusText = $"Subtotal: {subtotal:N2} Bs | Desc. items: {descuentoItems:N2} Bs | Desc. general: {descuentoGeneral:N2} Bs | Total: {total:N2} Bs | Items: {Items.Count}";
    }

    private static decimal ParsePositiveDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed) || decimal.TryParse(value, out parsed)
            ? Math.Max(parsed, 0)
            : 0;
    }

    private void ResolveProductSelection(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _matchedProduct = null;
            IsProductDropdownOpen = false;
            MatchedProductText = "Busca por codigo de barras, codigo o nombre.";
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
            SelectMatchedProduct(exactCode, "Coincidencia por codigo.");
            return;
        }

        ProductOption? exactName = Products.FirstOrDefault(p => p.Nombre.Trim().Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactName is not null)
        {
            SelectMatchedProduct(exactName, "Coincidencia exacta por nombre.");
            return;
        }

        List<ProductOption> matches = Products.Where(p => p.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 1)
        {
            SelectMatchedProduct(matches[0], "Coincidencia unica por nombre.");
            return;
        }

        _matchedProduct = null;
        SelectedProduct = matches.FirstOrDefault();
        IsProductDropdownOpen = matches.Count > 1;
        MatchedProductText = matches.Count > 1
            ? $"Se encontraron {matches.Count} coincidencias. Sigue escribiendo o elige manualmente."
            : "No se encontro producto con ese criterio.";
    }

    private void SelectMatchedProduct(ProductOption product, string message)
    {
        _matchedProduct = product;
        SelectedProduct = product;
        IsProductDropdownOpen = false;
        MatchedProductText = $"{product.Codigo} | {product.Nombre} | Stock: {product.StockDisponible} | {message}";
    }

    private void LoadSelectedItemIntoEditor(SaleNoteEditDetailModel? item)
    {
        if (item is null)
        {
            ItemActionText = "Agregar item";
            return;
        }

        ProductOption? product = item.IsKit
            ? Products.FirstOrDefault(p => p.IsKit && p.IdKit == item.IdKit)
            : Products.FirstOrDefault(p => !p.IsKit && p.IdProducto == item.IdProducto);
        _matchedProduct = product;
        SelectedProduct = product;
        SearchText = item.Codigo;
        Quantity = item.Cantidad.ToString(CultureInfo.InvariantCulture);
        Price = item.PrecioUnitario.ToString("0.##", CultureInfo.InvariantCulture);
        Discount = item.DescuentoMonto.ToString("0.##", CultureInfo.InvariantCulture);
        MatchedProductText = $"{item.Codigo} | {item.Producto} | Editando item seleccionado.";
        ItemActionText = "Guardar item";
    }

    private void ResetItemEditor()
    {
        SelectedItem = null;
        _matchedProduct = null;
        SelectedProduct = null;
        SearchText = string.Empty;
        Quantity = "1";
        Price = "0";
        Discount = "0";
        MatchedProductText = "Busca por codigo de barras, codigo o nombre.";
        ItemActionText = "Agregar item";
    }
}







