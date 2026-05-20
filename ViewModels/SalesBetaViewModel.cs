using System.Collections.ObjectModel;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class SalesBetaViewModel : ViewModelBase
{
    private readonly SalesBetaRepository _repository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserRepository _userRepository;
    private readonly IDialogService _dialogService;
    private DateTime? _fromDate = DateTime.Today.AddDays(-7);
    private DateTime? _toDate = DateTime.Today;
    private WarehouseItem? _selectedWarehouse;
    private AppUserListItem? _selectedUser;
    private string _productsText = "0 productos";
    private string _salesAmountText = "Bs 0.00";
    private string _costAmountText = "Bs 0.00";
    private string _profitAmountText = "Bs 0.00";
    private string _averageMarginText = "0.00 %";
    private string _statusText = "Sin datos cargados.";

    public SalesBetaViewModel(SalesBetaRepository repository, IInventoryRepository inventoryRepository, UserRepository userRepository, IDialogService dialogService)
    {
        _repository = repository;
        _inventoryRepository = inventoryRepository;
        _userRepository = userRepository;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        TodayCommand = new RelayCommand(SetTodayRange);
        ThisMonthCommand = new RelayCommand(SetThisMonthRange);
    }

    public ObservableCollection<WarehouseItem> Warehouses { get; } = new();
    public ObservableCollection<AppUserListItem> Users { get; } = new();
    public ObservableCollection<SalesBetaReportItem> Sales { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand TodayCommand { get; }
    public RelayCommand ThisMonthCommand { get; }

    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public WarehouseItem? SelectedWarehouse { get => _selectedWarehouse; set => SetProperty(ref _selectedWarehouse, value); }
    public AppUserListItem? SelectedUser { get => _selectedUser; set => SetProperty(ref _selectedUser, value); }
    public string ProductsText { get => _productsText; set => SetProperty(ref _productsText, value); }
    public string SalesAmountText { get => _salesAmountText; set => SetProperty(ref _salesAmountText, value); }
    public string CostAmountText { get => _costAmountText; set => SetProperty(ref _costAmountText, value); }
    public string ProfitAmountText { get => _profitAmountText; set => SetProperty(ref _profitAmountText, value); }
    public string AverageMarginText { get => _averageMarginText; set => SetProperty(ref _averageMarginText, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public async Task InitializeAsync()
    {
        try
        {
            var warehouses = await _inventoryRepository.GetWarehousesAsync();
            ReplaceCollection(Warehouses, warehouses.Prepend(new WarehouseItem { IdAlmacen = 0, Nombre = "Todos" }));
            SelectedWarehouse = Warehouses.FirstOrDefault();

            var users = await _userRepository.GetAllAsync();
            ReplaceCollection(Users, users.Prepend(new AppUserListItem { IdUsuario = 0, Nombre = "Todos" }));
            SelectedUser = Users.FirstOrDefault();

            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo cargar el modulo contable beta.\n\nDetalle: {ex.Message}");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            DateTime from = FromDate ?? DateTime.Today.AddDays(-7);
            DateTime to = ToDate ?? DateTime.Today;
            List<SalesBetaReportItem> sales = await _repository.GetSalesAsync(from, to, SelectedWarehouseId, SelectedUserId);

            ReplaceCollection(Sales, sales);

            decimal totalVendido = sales.Sum(s => s.TotalVendido);
            decimal totalCosto = sales.Sum(s => s.CostoTotal);
            decimal totalGanancia = sales.Sum(s => s.GananciaTotal);
            decimal averageMargin = totalVendido <= 0 ? 0 : (totalGanancia / totalVendido) * 100m;

            ProductsText = $"{sales.Count} producto(s)";
            SalesAmountText = $"Bs {totalVendido:N2}";
            CostAmountText = $"Bs {totalCosto:N2}";
            ProfitAmountText = $"Bs {totalGanancia:N2}";
            AverageMarginText = $"{averageMargin:N2} %";
            StatusText = $"Contable beta por producto. {sales.Count} registro(s) del periodo seleccionado.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron cargar las ganancias por producto.\n\nDetalle: {ex.Message}");
        }
    }

    private void SetTodayRange()
    {
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
        AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync());
    }

    private void SetThisMonthRange()
    {
        DateTime today = DateTime.Today;
        FromDate = new DateTime(today.Year, today.Month, 1);
        ToDate = today;
        AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync());
    }

    private int? SelectedWarehouseId => SelectedWarehouse is not null && SelectedWarehouse.IdAlmacen > 0 ? SelectedWarehouse.IdAlmacen : null;
    private int? SelectedUserId => SelectedUser is not null && SelectedUser.IdUsuario > 0 ? SelectedUser.IdUsuario : null;

}
