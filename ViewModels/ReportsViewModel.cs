using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly ReportsRepository _repository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserRepository _userRepository;
    private readonly ClientRepository _clientRepository;
    private readonly IDialogService _dialogService;
    private DateTime? _fromDate = DateTime.Today.AddDays(-30);
    private DateTime? _toDate = DateTime.Today;
    private WarehouseItem? _selectedWarehouse;
    private AppUserListItem? _selectedUser;
    private ClientOption? _selectedClient;
    private string _diasVentas = "0";
    private string _totalVendido = "0.00";
    private string _stockBajo = "0";
    private string _clientesVentas = "0";
    private string _movimientosStock = "0";

    public ReportsViewModel(ReportsRepository repository, IInventoryRepository inventoryRepository, UserRepository userRepository, ClientRepository clientRepository, IDialogService dialogService)
    {
        _repository = repository;
        _inventoryRepository = inventoryRepository;
        _userRepository = userRepository;
        _clientRepository = clientRepository;
        _dialogService = dialogService;

        GenerarCommand = new AsyncRelayCommand(LoadAsync);
        ImprimirCommand = new RelayCommand(ImprimirResumen);
        ExportAllPdfCommand = new RelayCommand(() => ExportAllPackage("pdf"));
        ExportAllExcelCommand = new RelayCommand(() => ExportAllPackage("xlsx"));
        ExportVentasDiaCommand = new RelayCommand(() => TryExport(VentasPorDia.ToList(), "Ventas por Dia", ReportExportService.ExportSalesByDayToPdf, ReportExportService.ExportSalesByDayToExcel));
        ExportTopProductosCommand = new RelayCommand(() => TryExport(TopProductos.ToList(), "Top Productos", ReportExportService.ExportTopProductsToPdf, ReportExportService.ExportTopProductsToExcel));
        ExportProductSalesCommand = new RelayCommand(() => TryExport(VentasPorProducto.ToList(), "Ventas por Producto", ReportExportService.ExportProductSalesToPdf, ReportExportService.ExportProductSalesToExcel));
        ExportStockBajoCommand = new RelayCommand(() => TryExport(StockBajoItems.ToList(), "Stock Bajo", ReportExportService.ExportLowStockToPdf, ReportExportService.ExportLowStockToExcel));
        ExportSalesByClientCommand = new RelayCommand(() => TryExport(VentasPorCliente.ToList(), "Ventas por Cliente", ReportExportService.ExportSalesByClientToPdf, ReportExportService.ExportSalesByClientToExcel));
        ExportInventoryMovementsCommand = new RelayCommand(() => TryExport(MovimientosInventario.ToList(), "Movimientos Inventario", ReportExportService.ExportInventoryMovementsToPdf, ReportExportService.ExportInventoryMovementsToExcel));
    }

    public ObservableCollection<WarehouseItem> Warehouses { get; } = new();
    public ObservableCollection<AppUserListItem> Users { get; } = new();
    public ObservableCollection<ClientOption> Clients { get; } = new();
    public ObservableCollection<SalesByDayReportItem> VentasPorDia { get; } = new();
    public ObservableCollection<TopProductReportItem> TopProductos { get; } = new();
    public ObservableCollection<ProductSalesReportItem> VentasPorProducto { get; } = new();
    public ObservableCollection<LowStockReportItem> StockBajoItems { get; } = new();
    public ObservableCollection<SalesByClientReportItem> VentasPorCliente { get; } = new();
    public ObservableCollection<InventoryMovementReportItem> MovimientosInventario { get; } = new();

    public AsyncRelayCommand GenerarCommand { get; }
    public RelayCommand ImprimirCommand { get; }
    public RelayCommand ExportAllPdfCommand { get; }
    public RelayCommand ExportAllExcelCommand { get; }
    public RelayCommand ExportVentasDiaCommand { get; }
    public RelayCommand ExportTopProductosCommand { get; }
    public RelayCommand ExportProductSalesCommand { get; }
    public RelayCommand ExportStockBajoCommand { get; }
    public RelayCommand ExportSalesByClientCommand { get; }
    public RelayCommand ExportInventoryMovementsCommand { get; }

    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public WarehouseItem? SelectedWarehouse { get => _selectedWarehouse; set => SetProperty(ref _selectedWarehouse, value); }
    public AppUserListItem? SelectedUser { get => _selectedUser; set => SetProperty(ref _selectedUser, value); }
    public ClientOption? SelectedClient { get => _selectedClient; set => SetProperty(ref _selectedClient, value); }
    public string DiasVentas { get => _diasVentas; set => SetProperty(ref _diasVentas, value); }
    public string TotalVendido { get => _totalVendido; set => SetProperty(ref _totalVendido, value); }
    public string StockBajo { get => _stockBajo; set => SetProperty(ref _stockBajo, value); }
    public string ClientesVentas { get => _clientesVentas; set => SetProperty(ref _clientesVentas, value); }
    public string MovimientosStock { get => _movimientosStock; set => SetProperty(ref _movimientosStock, value); }

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

            var clients = await _clientRepository.GetOptionsAsync();
            ReplaceCollection(Clients, clients.Prepend(new ClientOption { IdCliente = 0, Nombre = "Todos" }));
            SelectedClient = Clients.FirstOrDefault();

            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron cargar los reportes.\n\nDetalle: {ex.Message}", "Error de reportes");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            DateTime from = FromDate ?? DateTime.Today.AddDays(-30);
            DateTime to = ToDate ?? DateTime.Today;

            var bundle = await _repository.GetAllReportsAsync(from, to, SelectedWarehouseId, SelectedUserId, SelectedClientId);

            ReplaceCollection(VentasPorDia, bundle.SalesByDay);
            ReplaceCollection(TopProductos, bundle.TopProducts);
            ReplaceCollection(VentasPorProducto, bundle.ProductSales);
            ReplaceCollection(StockBajoItems, bundle.LowStock);
            ReplaceCollection(VentasPorCliente, bundle.SalesByClient);
            ReplaceCollection(MovimientosInventario, bundle.InventoryMovements);

            DiasVentas = bundle.SalesByDay.Count.ToString();
            TotalVendido = bundle.SalesByDay.Sum(x => x.TotalVendido).ToString("N2");
            StockBajo = bundle.LowStock.Count.ToString();
            ClientesVentas = bundle.SalesByClient.Count.ToString();
            MovimientosStock = bundle.InventoryMovements.Count.ToString();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudieron generar los reportes.\n\nDetalle: {ex.Message}", "Error de reportes");
        }
    }

    private void ImprimirResumen()
    {
        _dialogService.ShowInfo("La impresión visual directa se reemplazó por exportación. Usa PDF por tarjeta o paquete PDF.", "Impresión");
    }

    private void ExportAllPackage(string extension)
    {
        try
        {
            string zipPath = ReportExportService.GetExportPath($"Paquete_Reportes_{extension}");
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                return;
            }

            if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                zipPath = Path.ChangeExtension(zipPath, ".zip");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"campero-reportes-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                if (extension == "pdf")
                {
                    if (VentasPorDia.Any()) ReportExportService.ExportSalesByDayToPdf(VentasPorDia.ToList(), Path.Combine(tempDir, "VentasDia.pdf"));
                    if (TopProductos.Any()) ReportExportService.ExportTopProductsToPdf(TopProductos.ToList(), Path.Combine(tempDir, "TopProductos.pdf"));
                    if (VentasPorProducto.Any()) ReportExportService.ExportProductSalesToPdf(VentasPorProducto.ToList(), Path.Combine(tempDir, "VentasProducto.pdf"));
                    if (StockBajoItems.Any()) ReportExportService.ExportLowStockToPdf(StockBajoItems.ToList(), Path.Combine(tempDir, "StockBajo.pdf"));
                    if (VentasPorCliente.Any()) ReportExportService.ExportSalesByClientToPdf(VentasPorCliente.ToList(), Path.Combine(tempDir, "VentasCliente.pdf"));
                    if (MovimientosInventario.Any()) ReportExportService.ExportInventoryMovementsToPdf(MovimientosInventario.ToList(), Path.Combine(tempDir, "MovimientosInventario.pdf"));
                }
                else
                {
                    if (VentasPorDia.Any()) ReportExportService.ExportSalesByDayToExcel(VentasPorDia.ToList(), Path.Combine(tempDir, "VentasDia.xlsx"));
                    if (TopProductos.Any()) ReportExportService.ExportTopProductsToExcel(TopProductos.ToList(), Path.Combine(tempDir, "TopProductos.xlsx"));
                    if (VentasPorProducto.Any()) ReportExportService.ExportProductSalesToExcel(VentasPorProducto.ToList(), Path.Combine(tempDir, "VentasProducto.xlsx"));
                    if (StockBajoItems.Any()) ReportExportService.ExportLowStockToExcel(StockBajoItems.ToList(), Path.Combine(tempDir, "StockBajo.xlsx"));
                    if (VentasPorCliente.Any()) ReportExportService.ExportSalesByClientToExcel(VentasPorCliente.ToList(), Path.Combine(tempDir, "VentasCliente.xlsx"));
                    if (MovimientosInventario.Any()) ReportExportService.ExportInventoryMovementsToExcel(MovimientosInventario.ToList(), Path.Combine(tempDir, "MovimientosInventario.xlsx"));
                }

                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempDir, zipPath);
                _dialogService.ShowInfo("Paquete consolidado generado correctamente.", "Exito");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo generar el paquete consolidado.\n\nDetalle: {ex.Message}", "Error de exportacion");
        }
    }

    private void TryExport<T>(List<T> data, string title, Action<List<T>, string> pdfExporter, Action<List<T>, string> excelExporter)
    {
        try
        {
            if (data is null || data.Count == 0)
            {
                _dialogService.ShowInfo("No hay datos para exportar.");
                return;
            }

            string path = ReportExportService.GetExportPath(title);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) pdfExporter(data, path);
            else if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) excelExporter(data, path);
            else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowInfo("Para paquete consolidado usa los botones 'Paquete PDF' o 'Paquete Excel'.");
                return;
            }

            _dialogService.ShowInfo("Reporte exportado correctamente.", "Exito");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo exportar el reporte.\n\nDetalle: {ex.Message}", "Error de exportacion");
        }
    }

    private int? SelectedWarehouseId => SelectedWarehouse is not null && SelectedWarehouse.IdAlmacen > 0 ? SelectedWarehouse.IdAlmacen : null;
    private int? SelectedUserId => SelectedUser is not null && SelectedUser.IdUsuario > 0 ? SelectedUser.IdUsuario : null;
    private int? SelectedClientId => SelectedClient is not null && SelectedClient.IdCliente > 0 ? SelectedClient.IdCliente : null;
}
