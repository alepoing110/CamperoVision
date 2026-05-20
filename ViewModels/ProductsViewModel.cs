using System.Collections.ObjectModel;
using System.Globalization;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class ProductsViewModel : ValidatableViewModelBase
{
    private readonly ProductRepository _repository;
    private readonly IDialogService _dialogService;
    private readonly List<CategoryItem> _categories = new();
    private int _editingId;
    private string _codigo = string.Empty;
    private string _codigoBarras = string.Empty;
    private string _nombre = string.Empty;
    private string _precioCompra = "0";
    private string _precioVenta = "0";
    private string _stockMinimo = "0";
    private string _buscar = string.Empty;
    private string _estado = string.Empty;
    private string _formStatus = "Nuevo producto";
    private int _barcodeValidationVersion;
    private string _lastDuplicateBarcodeWarning = string.Empty;
    private CategoryItem? _selectedCategory;
    private ProductListItem? _selectedProduct;

    public ProductsViewModel(ProductRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);
        BuscarCommand = new AsyncRelayCommand(LoadAsync);
        NuevoCommand = new RelayCommand(ResetForm);
    }

    public ObservableCollection<CategoryItem> Categories { get; } = new();
    public ObservableCollection<ProductListItem> Products { get; } = new();
    public AsyncRelayCommand GuardarCommand { get; }
    public AsyncRelayCommand BuscarCommand { get; }
    public RelayCommand NuevoCommand { get; }

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

    public string CodigoBarras
    {
        get => _codigoBarras;
        set
        {
            if (SetProperty(ref _codigoBarras, value))
            {
                ValidateCodigoBarras();
                AsyncHelper.FireAndForgetOnUi(async () => await ValidateBarcodeDuplicateAsync());
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

    public string PrecioCompra
    {
        get => _precioCompra;
        set
        {
            if (SetProperty(ref _precioCompra, value))
            {
                ValidatePrecioCompra();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string PrecioVenta
    {
        get => _precioVenta;
        set
        {
            if (SetProperty(ref _precioVenta, value))
            {
                ValidatePrecioVenta();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StockMinimo
    {
        get => _stockMinimo;
        set
        {
            if (SetProperty(ref _stockMinimo, value))
            {
                ValidateStockMinimo();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string Buscar { get => _buscar; set => SetProperty(ref _buscar, value); }
    public string Estado { get => _estado; set => SetProperty(ref _estado, value); }
    public string FormStatus { get => _formStatus; set => SetProperty(ref _formStatus, value); }

    public CategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ValidateCategory();
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ProductListItem? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value) && value is not null)
            {
                _editingId = value.IdProducto;
                Codigo = value.Codigo;
                CodigoBarras = value.CodigoBarras;
                Nombre = value.Nombre;
                PrecioCompra = value.PrecioCompra.ToString(CultureInfo.InvariantCulture);
                PrecioVenta = value.PrecioVenta.ToString(CultureInfo.InvariantCulture);
                StockMinimo = value.StockMinimo.ToString();
                SelectedCategory = _categories.FirstOrDefault(c => c.Nombre == value.Categoria);
                FormStatus = $"Editando producto ID {_editingId}: {value.Nombre}";
            }
        }
    }

    public async Task InitializeAsync()
    {
        _categories.Clear();
        Categories.Clear();
        var categories = await _repository.GetCategoriesAsync();
        foreach (var category in categories)
        {
            _categories.Add(category);
            Categories.Add(category);
        }
        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
        }
        await LoadAsync();
        ResetForm();
    }

    public async Task LoadAsync()
    {
        var items = await _repository.GetAllAsync(Buscar.Trim());
        ReplaceCollection(Products, items);
        Estado = $"{items.Count} producto(s)";
    }

    private async Task GuardarAsync()
    {
        ValidateForm();
        if (HasErrors || SelectedCategory is null)
        {
            _dialogService.ShowWarning("Corrige los datos del producto antes de guardar.");
            return;
        }

        if (!DecimalParser.TryParse(PrecioCompra, out decimal compra))
        {
            _dialogService.ShowWarning("Precio de compra invalido.");
            return;
        }

        if (!DecimalParser.TryParse(PrecioVenta, out decimal venta))
        {
            _dialogService.ShowWarning("Precio de venta invalido.");
            return;
        }

        if (!int.TryParse(StockMinimo, out int stockMinimo))
        {
            _dialogService.ShowWarning("Stock minimo invalido.");
            return;
        }

        ProductUpsertModel model = new()
        {
            IdProducto = _editingId,
            IdCategoria = SelectedCategory.IdCategoria,
            Codigo = Codigo.Trim(),
            CodigoBarras = CodigoBarras.Trim(),
            Nombre = Nombre.Trim(),
            PrecioCompra = compra,
            PrecioVenta = venta,
            StockMinimo = stockMinimo,
            Activo = true
        };

        ProductRepository.ProductDuplicateCheckResult duplicates = await _repository.CheckDuplicatesAsync(model);
        ApplyDuplicateErrors(duplicates);
        if (HasErrors)
        {
            _dialogService.ShowWarning("No se puede guardar porque hay datos repetidos o inconsistentes en el producto.");
            return;
        }

        await _repository.SaveAsync(model);

        await LoadAsync();
        Estado = _editingId == 0
            ? "Producto creado correctamente."
            : "Producto actualizado correctamente.";
        ResetForm();
    }

    private void ResetForm()
    {
        _editingId = 0;
        _selectedProduct = null;
        OnPropertyChanged(nameof(SelectedProduct));
        Codigo = string.Empty;
        CodigoBarras = string.Empty;
        _lastDuplicateBarcodeWarning = string.Empty;
        Nombre = string.Empty;
        PrecioCompra = "0";
        PrecioVenta = "0";
        StockMinimo = "0";
        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
        }

        ClearAllErrors();
        FormStatus = "Nuevo producto";
        GuardarCommand.RaiseCanExecuteChanged();
    }

    private bool CanSave() => !HasErrors && !string.IsNullOrWhiteSpace(Codigo) && !string.IsNullOrWhiteSpace(Nombre) && SelectedCategory is not null;

    private void ValidateForm()
    {
        ValidateCodigo();
        ValidateCodigoBarras();
        ValidateNombre();
        ValidateCategory();
        ValidatePrecioCompra();
        ValidatePrecioVenta();
        ValidateStockMinimo();
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

    private void ValidateCodigoBarras()
    {
        if (!string.IsNullOrWhiteSpace(CodigoBarras) && CodigoBarras.Trim().Length < 3)
        {
            SetErrors(nameof(CodigoBarras), new[] { "El codigo de barras es demasiado corto." });
            return;
        }

        ClearErrors(nameof(CodigoBarras));
    }

    private async Task ValidateBarcodeDuplicateAsync()
    {
        int validationVersion = ++_barcodeValidationVersion;
        string barcode = CodigoBarras.Trim();

        if (string.IsNullOrWhiteSpace(barcode))
        {
            ValidateCodigoBarras();
            GuardarCommand.RaiseCanExecuteChanged();
            return;
        }

        if (barcode.Length < 3)
        {
            GuardarCommand.RaiseCanExecuteChanged();
            return;
        }

        bool exists = await _repository.BarcodeExistsAsync(barcode, _editingId);
        if (validationVersion != _barcodeValidationVersion)
        {
            return;
        }

        if (exists)
        {
            SetErrors(nameof(CodigoBarras), new[] { "Ya existe un producto con ese codigo de barras." });
            if (!string.Equals(_lastDuplicateBarcodeWarning, barcode, StringComparison.OrdinalIgnoreCase))
            {
                _lastDuplicateBarcodeWarning = barcode;
                _dialogService.ShowError($"El codigo de barras '{barcode}' ya existe en otro producto.", "Codigo duplicado");
            }
        }
        else
        {
            _lastDuplicateBarcodeWarning = string.Empty;
            ClearErrors(nameof(CodigoBarras));
            ValidateCodigoBarras();
        }

        GuardarCommand.RaiseCanExecuteChanged();
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

    private void ValidateCategory()
    {
        if (SelectedCategory is null)
        {
            SetErrors(nameof(SelectedCategory), new[] { "Selecciona una categoria." });
            return;
        }

        ClearErrors(nameof(SelectedCategory));
    }

    private void ValidatePrecioCompra()
    {
        if (!DecimalParser.TryParsePositive(PrecioCompra, out _))
        {
            SetErrors(nameof(PrecioCompra), new[] { "Ingresa un precio de compra valido." });
            return;
        }

        ClearErrors(nameof(PrecioCompra));
    }

    private void ValidatePrecioVenta()
    {
        if (!DecimalParser.TryParsePositive(PrecioVenta, out _))
        {
            SetErrors(nameof(PrecioVenta), new[] { "Ingresa un precio de venta valido." });
            return;
        }

        ClearErrors(nameof(PrecioVenta));
    }

    private void ValidateStockMinimo()
    {
        if (!int.TryParse(StockMinimo, out int stockMinimo) || stockMinimo < 0)
        {
            SetErrors(nameof(StockMinimo), new[] { "Ingresa un stock minimo valido." });
            return;
        }

        ClearErrors(nameof(StockMinimo));
    }

    private void ApplyDuplicateErrors(ProductRepository.ProductDuplicateCheckResult duplicates)
    {
        if (duplicates.IdConflict)
        {
            SetErrors(nameof(SelectedProduct), new[] { "El producto que intentas modificar ya no existe o su ID no es valido." });
        }
        else
        {
            ClearErrors(nameof(SelectedProduct));
        }

        if (duplicates.CodigoConflict)
        {
            SetErrors(nameof(Codigo), new[] { "Ya existe un producto con ese codigo." });
        }
        else if (!GetErrors(nameof(Codigo)).Cast<string>().Any(error => error.Contains("obligatorio", StringComparison.OrdinalIgnoreCase)))
        {
            ClearErrors(nameof(Codigo));
            ValidateCodigo();
        }

        if (duplicates.CodigoBarrasConflict)
        {
            SetErrors(nameof(CodigoBarras), new[] { "Ya existe un producto con ese codigo de barras." });
        }
        else
        {
            ClearErrors(nameof(CodigoBarras));
            ValidateCodigoBarras();
        }

        if (duplicates.NombreConflict)
        {
            SetErrors(nameof(Nombre), new[] { "Ya existe un producto con ese nombre." });
        }
        else if (!GetErrors(nameof(Nombre)).Cast<string>().Any(error => error.Contains("obligatorio", StringComparison.OrdinalIgnoreCase)))
        {
            ClearErrors(nameof(Nombre));
            ValidateNombre();
        }
    }
}
