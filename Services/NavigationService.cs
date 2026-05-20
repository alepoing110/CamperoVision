using System.Reflection;
using CamperoDesktop.Models;
using CamperoDesktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CamperoDesktop.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionService _sessionService;
    private readonly Dictionary<AppModule, ModuleRouteConfig> _routes;

    public NavigationService(IServiceProvider serviceProvider, ISessionService sessionService)
    {
        _serviceProvider = serviceProvider;
        _sessionService = sessionService;
        _routes = BuildRoutes();
    }

    public event EventHandler<ModuleNavigationState>? Navigated;

    public async Task NavigateToAsync(AppModule module)
    {
        UserSession currentUser = _sessionService.CurrentUser
            ?? throw new InvalidOperationException("No existe una sesion activa para navegar.");

        if (!_routes.TryGetValue(module, out var config))
        {
            throw new ArgumentOutOfRangeException(nameof(module), module, "Modulo no soportado.");
        }

        var viewModel = config.RequiresUser
            ? ActivatorUtilities.CreateInstance(_serviceProvider, config.ViewModelType, currentUser)
            : _serviceProvider.GetRequiredService(config.ViewModelType);

        await config.InvokeInitializeAsync(viewModel);

        var state = new ModuleNavigationState
        {
            Module = module,
            Title = config.Title,
            Description = config.Description,
            Content = viewModel
        };

        Navigated?.Invoke(this, state);
    }

    private static Dictionary<AppModule, ModuleRouteConfig> BuildRoutes() => new()
    {
        [AppModule.Dashboard] = new(typeof(DashboardViewModel), "Dashboard", "Resumen general del sistema", nameof(DashboardViewModel.LoadAsync), true),
        [AppModule.Productos] = new(typeof(ProductsViewModel), "Productos", "CRUD de productos y categorias", nameof(ProductsViewModel.InitializeAsync)),
        [AppModule.Kits] = new(typeof(KitsViewModel), "Kits", "Productos compuestos para ventas y cotizaciones", nameof(KitsViewModel.InitializeAsync)),
        [AppModule.Categorias] = new(typeof(CategoriesViewModel), "Categorias", "CRUD de categorias para organizar productos", nameof(CategoriesViewModel.LoadAsync)),
        [AppModule.Clientes] = new(typeof(ClientsViewModel), "Clientes", "CRUD de clientes", nameof(ClientsViewModel.LoadAsync)),
        [AppModule.Ventas] = new(typeof(SalesNotesViewModel), "Notas de Venta", "Registro de ventas y detalle", nameof(SalesNotesViewModel.InitializeAsync), true),
        [AppModule.Inventario] = new(typeof(InventoryViewModel), "Inventario", "Movimientos, stock y kardex por almacen", nameof(InventoryViewModel.LoadAsync), true),
        [AppModule.Almacenes] = new(typeof(WarehousesViewModel), "Almacenes", "CRUD de almacenes y desactivacion logica", nameof(WarehousesViewModel.LoadAsync)),
        [AppModule.Cotizaciones] = new(typeof(QuotationViewModel), "Cotizaciones", "Propuestas comerciales para clientes con PDF listo para imprimir", nameof(QuotationViewModel.InitializeAsync), true),
        [AppModule.VentasBeta] = new(typeof(SalesBetaViewModel), "Contable Beta", "Vista preliminar de ganancias por producto segun el periodo seleccionado", nameof(SalesBetaViewModel.InitializeAsync)),
        [AppModule.Negocio] = new(typeof(BusinessSettingsViewModel), "Configuracion del negocio", "Datos institucionales para reportes, notas y cotizaciones", nameof(BusinessSettingsViewModel.InitializeAsync)),
        [AppModule.Usuarios] = new(typeof(UsersViewModel), "Usuarios", "Usuarios, roles y acceso", nameof(UsersViewModel.LoadAsync)),
        [AppModule.Reportes] = new(typeof(ReportsViewModel), "Reportes", "Ventas, clientes, inventario y exportacion PDF/Excel", nameof(ReportsViewModel.InitializeAsync)),
        [AppModule.Proveedores] = new(typeof(ProvidersViewModel), "Proveedores", "CRUD Proveedores", nameof(ProvidersViewModel.LoadAsync)),
        [AppModule.Compras] = new(typeof(PurchasesViewModel), "Compras", "Ordenes de compra por almacen", nameof(PurchasesViewModel.LoadAsync), true),
        [AppModule.Backup] = new(typeof(BackupViewModel), "Backup y Restauracion", "Copias de seguridad y restauracion de la base de datos", null),
        [AppModule.Auditoria] = new(typeof(AuditViewModel), "Auditoria de Cambios", "Registro de todas las acciones realizadas en el sistema", null),
    };
}

public record ModuleRouteConfig(Type ViewModelType, string Title, string Description, string? InitializeMethodName, bool RequiresUser = false)
{
    public async Task InvokeInitializeAsync(object viewModel)
    {
        if (string.IsNullOrEmpty(InitializeMethodName))
        {
            return;
        }

        var method = ViewModelType.GetMethod(InitializeMethodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Metodo {InitializeMethodName} no encontrado en {ViewModelType.Name}");

        var task = method.Invoke(viewModel, null) as Task;
        if (task != null) await task;
    }
}
