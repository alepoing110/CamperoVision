using CamperoDesktop.Data;
using CamperoDesktop.Commands;
using CamperoDesktop.Models;
using System.Collections.ObjectModel;
using System.Windows;
using CamperoDesktop.Views;

namespace CamperoDesktop.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly DashboardRepository _repository;
    private readonly UserSession _currentUser;
    private int _totalProductos;
    private int _totalClientes;
    private int _totalAlmacenes;
    private int _totalNotasVenta;
    private int _totalProductosStockMinimo;
    private decimal _ventasHoy;
    private decimal _ventasMes;
    private int _notasEmitidasHoy;
    private string _descripcion = string.Empty;
    private string _lowStockStatus = "Sin alertas";

    public DashboardViewModel(DashboardRepository repository, UserSession currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
        NombreUsuario = currentUser.Nombre;
        Usuario = currentUser.Usuario;
        Rol = currentUser.Rol;
        Descripcion = $"Sesion iniciada como {currentUser.Rol}. Desde aqui puedes entrar a los modulos disponibles segun tu permiso.";
        ShowLowStockProductsCommand = new RelayCommand(ShowLowStockProducts);
    }

    public string NombreUsuario { get; }
    public string Usuario { get; }
    public string Rol { get; }
    public ObservableCollection<LowStockReportItem> LowStockProducts { get; } = new();
    public RelayCommand ShowLowStockProductsCommand { get; }

    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    public int TotalProductos
    {
        get => _totalProductos;
        set => SetProperty(ref _totalProductos, value);
    }

    public int TotalClientes
    {
        get => _totalClientes;
        set => SetProperty(ref _totalClientes, value);
    }

    public int TotalAlmacenes
    {
        get => _totalAlmacenes;
        set => SetProperty(ref _totalAlmacenes, value);
    }

    public int TotalNotasVenta
    {
        get => _totalNotasVenta;
        set => SetProperty(ref _totalNotasVenta, value);
    }

    public int TotalProductosStockMinimo
    {
        get => _totalProductosStockMinimo;
        set => SetProperty(ref _totalProductosStockMinimo, value);
    }

    public decimal VentasHoy
    {
        get => _ventasHoy;
        set => SetProperty(ref _ventasHoy, value);
    }

    public decimal VentasMes
    {
        get => _ventasMes;
        set => SetProperty(ref _ventasMes, value);
    }

    public int NotasEmitidasHoy
    {
        get => _notasEmitidasHoy;
        set => SetProperty(ref _notasEmitidasHoy, value);
    }

    public string LowStockStatus
    {
        get => _lowStockStatus;
        set => SetProperty(ref _lowStockStatus, value);
    }

    public async Task LoadAsync()
    {
        DashboardSummary summary = await _repository.GetSummaryAsync();
        TotalProductos = summary.TotalProductos;
        TotalClientes = summary.TotalClientes;
        TotalAlmacenes = summary.TotalAlmacenes;
        TotalNotasVenta = summary.TotalNotasVenta;
        VentasHoy = summary.VentasHoy;
        VentasMes = summary.VentasMes;
        NotasEmitidasHoy = summary.NotasEmitidasHoy;

        List<LowStockReportItem> lowStockItems = await _repository.GetLowStockProductsAsync();
        ReplaceCollection(LowStockProducts, lowStockItems);

        TotalProductosStockMinimo = lowStockItems.Count;
        LowStockStatus = lowStockItems.Count == 0
            ? "No hay productos en stock minimo."
            : $"{lowStockItems.Count} producto(s) requieren atencion.";
    }

    private void ShowLowStockProducts()
    {
        LowStockProductsWindow window = new(LowStockProducts.ToList());
        window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        window.ShowDialog();
    }
}
