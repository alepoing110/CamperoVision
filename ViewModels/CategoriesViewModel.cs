using System.Collections.ObjectModel;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using CamperoDesktop.Views;
using System.Windows;

namespace CamperoDesktop.ViewModels;

public class CategoriesViewModel : ValidatableViewModelBase
{
    private readonly CategoryRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IDialogService _dialogService;
    private int _editingId;
    private string _nombre = string.Empty;
    private string _descripcion = string.Empty;
    private string _buscar = string.Empty;
    private string _estado = string.Empty;
    private CategoryListItem? _selectedCategory;

    public CategoriesViewModel(CategoryRepository repository, IProductRepository productRepository, IDialogService dialogService)
    {
        _repository = repository;
        _productRepository = productRepository;
        _dialogService = dialogService;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);
        BuscarCommand = new AsyncRelayCommand(LoadAsync);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => SelectedCategory is not null);
        NuevoCommand = new RelayCommand(ResetForm);
    }

    public ObservableCollection<CategoryListItem> Categories { get; } = new();
    public AsyncRelayCommand GuardarCommand { get; }
    public AsyncRelayCommand BuscarCommand { get; }
    public AsyncRelayCommand EliminarCommand { get; }
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

    public string Descripcion { get => _descripcion; set => SetProperty(ref _descripcion, value); }
    public string Buscar { get => _buscar; set => SetProperty(ref _buscar, value); }
    public string Estado { get => _estado; set => SetProperty(ref _estado, value); }

    public CategoryListItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value) && value is not null)
            {
                _editingId = value.IdCategoria;
                Nombre = value.Nombre;
                Descripcion = value.Descripcion;
                AsyncHelper.FireAndForgetOnUi(async () => await ShowCategoryProductsAsync(value));
            }

            EliminarCommand.RaiseCanExecuteChanged();
        }
    }

    public async Task LoadAsync()
    {
        List<CategoryListItem> items = await _repository.GetAllAsync(Buscar.Trim());
        ReplaceCollection(Categories, items);
        Estado = $"{items.Count} categoria(s)";
    }

    private async Task GuardarAsync()
    {
        ValidateNombre();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los datos de la categoria antes de guardar.");
            return;
        }

        await _repository.SaveAsync(new CategoryUpsertModel
        {
            IdCategoria = _editingId,
            Nombre = Nombre.Trim(),
            Descripcion = Descripcion.Trim(),
            Activo = true
        });

        await LoadAsync();
        ResetForm();
    }

    private async Task EliminarAsync()
    {
        if (SelectedCategory is null)
        {
            _dialogService.ShowWarning("Selecciona una categoria para eliminar.");
            return;
        }

        bool hasProducts = await _repository.HasProductsAsync(SelectedCategory.IdCategoria);
        if (hasProducts)
        {
            _dialogService.ShowWarning("No se puede eliminar la categoria porque ya esta siendo usada por uno o mas productos.");
            return;
        }

        string categoryName = SelectedCategory.Nombre;
        await _repository.DeleteAsync(SelectedCategory.IdCategoria);
        await LoadAsync();
        ResetForm();
        _dialogService.ShowInfo($"Categoria {categoryName} eliminada correctamente.");
    }

    private async Task ShowCategoryProductsAsync(CategoryListItem category)
    {
        try
        {
            List<ProductListItem> products = await _productRepository.GetByCategoryAsync(category.IdCategoria);
            CategoryProductsWindow window = new(category.Nombre, products);
            window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron cargar los productos de la categoria.\n\nDetalle: {ex.Message}");
        }
    }

    private void ResetForm()
    {
        _editingId = 0;
        _selectedCategory = null;
        OnPropertyChanged(nameof(SelectedCategory));
        Nombre = string.Empty;
        Descripcion = string.Empty;
        ClearAllErrors();
        GuardarCommand.RaiseCanExecuteChanged();
        EliminarCommand.RaiseCanExecuteChanged();
    }

    private bool CanSave() => !HasErrors && !string.IsNullOrWhiteSpace(Nombre);

    private void ValidateNombre()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            SetErrors(nameof(Nombre), new[] { "El nombre de la categoria es obligatorio." });
            return;
        }

        ClearErrors(nameof(Nombre));
    }
}
