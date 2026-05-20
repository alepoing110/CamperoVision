using System.Windows;
using CamperoDesktop.Commands;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IWindowService _windowService;
    private readonly IDialogService _dialogService;
    private string _tituloModulo = "Dashboard";
    private string _descripcionModulo = "Resumen general del sistema";
    private object? _currentContent;

    private static readonly Dictionary<AppModule, ModuleCommandConfig> ModuleConfigs = new()
    {
        [AppModule.Dashboard] = new("Dashboard", "Dashboard", "Resumen general del sistema", null),
        [AppModule.Productos] = new("Productos", "Productos", "CRUD de productos y categorias", new[] { UserRoles.Admin, UserRoles.Almacenero }),
        [AppModule.Kits] = new("Kits", "Kits", "Productos compuestos para ventas y cotizaciones", new[] { UserRoles.Admin, UserRoles.Almacenero }),
        [AppModule.Categorias] = new("Categorias", "Categorias", "CRUD de categorias para organizar productos", new[] { UserRoles.Admin, UserRoles.Almacenero }),
        [AppModule.Clientes] = new("Clientes", "Clientes", "CRUD de clientes", new[] { UserRoles.Admin, UserRoles.Vendedor }),
        [AppModule.Ventas] = new("Ventas", "Notas de Venta", "Registro de ventas y detalle", new[] { UserRoles.Admin, UserRoles.Vendedor }),
        [AppModule.Inventario] = new("Inventario", "Inventario", "Movimientos, stock y kardex por almacen", new[] { UserRoles.Admin, UserRoles.Almacenero }),
        [AppModule.Almacenes] = new("Almacenes", "Almacenes", "CRUD de almacenes y desactivacion logica", new[] { UserRoles.Admin, UserRoles.Almacenero }),
        [AppModule.Cotizaciones] = new("Cotizaciones", "Cotizaciones", "Propuestas comerciales para clientes con PDF listo para imprimir", new[] { UserRoles.Admin, UserRoles.Vendedor }),
        [AppModule.VentasBeta] = new("VentasBeta", "Contable Beta", "Vista preliminar de ganancias por producto segun el periodo seleccionado", new[] { UserRoles.Admin }),
        [AppModule.Negocio] = new("Negocio", "Configuracion del negocio", "Datos institucionales para reportes, notas y cotizaciones", new[] { UserRoles.Admin }),
        [AppModule.Usuarios] = new("Usuarios", "Usuarios", "Usuarios, roles y acceso", new[] { UserRoles.Admin }),
        [AppModule.Reportes] = new("Reportes", "Reportes", "Ventas, clientes, inventario y exportacion PDF/Excel", null),
        [AppModule.Proveedores] = new("Proveedores", "Proveedores", "CRUD Proveedores", null),
        [AppModule.Compras] = new("Compras", "Compras", "Ordenes de compra por almacen", new[] { UserRoles.Admin, UserRoles.Almacenero }),
        [AppModule.Backup] = new("Backup", "Backup y Restauracion", "Copias de seguridad y restauracion de la base de datos", new[] { UserRoles.Admin }),
        [AppModule.Auditoria] = new("Auditoria", "Auditoria de Cambios", "Registro de todas las acciones realizadas en el sistema", new[] { UserRoles.Admin }),
    };

    public MainViewModel(ISessionService sessionService, INavigationService navigationService, IWindowService windowService, IDialogService dialogService)
    {
        _sessionService = sessionService;
        _navigationService = navigationService;
        _windowService = windowService;
        _dialogService = dialogService;
        _navigationService.Navigated += OnNavigated;

        UserSession currentUser = _sessionService.CurrentUser
            ?? throw new InvalidOperationException("No existe una sesion activa.");

        UsuarioActual = currentUser.Nombre;
        RolActual = currentUser.Rol;
        EstadoSesion = "Activa";

        NavigationCommands = new Dictionary<string, AsyncRelayCommand>();
        foreach (var (module, config) in ModuleConfigs)
        {
            NavigationCommands[config.PropertyName] = new AsyncRelayCommand(() => NavigateSafelyAsync(module, config.DisplayName));
        }

        CerrarSesionCommand = new RelayCommand(CerrarSesion);
    }

    public string UsuarioActual { get; }
    public string RolActual { get; }
    public string EstadoSesion { get; }
    public string TituloModulo { get => _tituloModulo; set => SetProperty(ref _tituloModulo, value); }
    public string DescripcionModulo { get => _descripcionModulo; set => SetProperty(ref _descripcionModulo, value); }
    public object? CurrentContent { get => _currentContent; set => SetProperty(ref _currentContent, value); }

    public Dictionary<string, AsyncRelayCommand> NavigationCommands { get; }
    public RelayCommand CerrarSesionCommand { get; }

    public Visibility GetVisibility(AppModule module)
    {
        if (!ModuleConfigs.TryGetValue(module, out var config) || config.AllowedRoles is null)
        {
            return Visibility.Visible;
        }
        return config.AllowedRoles.Contains(CurrentRole) ? Visibility.Visible : Visibility.Collapsed;
    }

    public Visibility UsuariosVisibility => GetVisibility(AppModule.Usuarios);
    public Visibility InventarioVisibility => GetVisibility(AppModule.Inventario);
    public Visibility AlmacenesVisibility => GetVisibility(AppModule.Almacenes);
    public Visibility VentasVisibility => GetVisibility(AppModule.Ventas);
    public Visibility CotizacionesVisibility => GetVisibility(AppModule.Cotizaciones);
    public Visibility VentasBetaVisibility => GetVisibility(AppModule.VentasBeta);
    public Visibility NegocioVisibility => GetVisibility(AppModule.Negocio);
    public Visibility ClientesVisibility => GetVisibility(AppModule.Clientes);
    public Visibility ProductosVisibility => GetVisibility(AppModule.Productos);
    public Visibility KitsVisibility => GetVisibility(AppModule.Kits);
    public Visibility CategoriasVisibility => GetVisibility(AppModule.Categorias);
    public Visibility ReportesVisibility => GetVisibility(AppModule.Reportes);
    public Visibility ComprasVisibility => GetVisibility(AppModule.Compras);
    public Visibility BackupVisibility => GetVisibility(AppModule.Backup);
    public Visibility AuditoriaVisibility => GetVisibility(AppModule.Auditoria);

    public AsyncRelayCommand ShowDashboardCommand => NavigationCommands["Dashboard"];
    public AsyncRelayCommand ShowProductosCommand => NavigationCommands["Productos"];
    public AsyncRelayCommand ShowKitsCommand => NavigationCommands["Kits"];
    public AsyncRelayCommand ShowCategoriasCommand => NavigationCommands["Categorias"];
    public AsyncRelayCommand ShowClientesCommand => NavigationCommands["Clientes"];
    public AsyncRelayCommand ShowVentasCommand => NavigationCommands["Ventas"];
    public AsyncRelayCommand ShowInventarioCommand => NavigationCommands["Inventario"];
    public AsyncRelayCommand ShowAlmacenesCommand => NavigationCommands["Almacenes"];
    public AsyncRelayCommand ShowCotizacionesCommand => NavigationCommands["Cotizaciones"];
    public AsyncRelayCommand ShowVentasBetaCommand => NavigationCommands["VentasBeta"];
    public AsyncRelayCommand ShowNegocioCommand => NavigationCommands["Negocio"];
    public AsyncRelayCommand ShowUsuariosCommand => NavigationCommands["Usuarios"];
    public AsyncRelayCommand ShowReportesCommand => NavigationCommands["Reportes"];
    public AsyncRelayCommand ShowProveedoresCommand => NavigationCommands["Proveedores"];
    public AsyncRelayCommand ShowComprasCommand => NavigationCommands["Compras"];
    public AsyncRelayCommand ShowBackupCommand => NavigationCommands["Backup"];
    public AsyncRelayCommand ShowAuditoriaCommand => NavigationCommands["Auditoria"];

    public async Task InitializeAsync() => await NavigateSafelyAsync(AppModule.Dashboard, "Dashboard");

    private void CerrarSesion() => _windowService.Logout();

    private async Task NavigateSafelyAsync(AppModule module, string moduleName)
    {
        try
        {
            if (!CanAccessModule(module))
            {
                _dialogService.ShowWarning($"No tienes permisos para abrir el modulo {moduleName}.");
                return;
            }
            await _navigationService.NavigateToAsync(module);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo abrir el modulo {moduleName}.\n\nDetalle: {ex.Message}");
        }
    }

    private string CurrentRole => (_sessionService.CurrentUser?.Rol ?? string.Empty).Trim().ToLowerInvariant();

    private bool CanAccessModule(AppModule module)
    {
        return !ModuleConfigs.TryGetValue(module, out var config) || config.AllowedRoles is null || config.AllowedRoles.Contains(CurrentRole);
    }

    private void OnNavigated(object? sender, ModuleNavigationState state)
    {
        TituloModulo = state.Title;
        DescripcionModulo = state.Description;
        CurrentContent = state.Content;
    }

    public void Dispose() => _navigationService.Navigated -= OnNavigated;
}

public record ModuleCommandConfig(string PropertyName, string DisplayName, string Description, string[]? AllowedRoles);
