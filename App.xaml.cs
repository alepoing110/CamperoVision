using System.Windows;
using CamperoDesktop.Data;
using CamperoDesktop.Services;
using CamperoDesktop.ViewModels;
using CamperoDesktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows.Threading;

namespace CamperoDesktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LoggingService.Configure();
        Log.Information("=== CamperoDesktop iniciando ===");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        ServiceCollection services = new();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        LoginWindow loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== CamperoDesktop cerrando ===");
        Log.CloseAndFlush();
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Error no controlado en dispatcher");
        MessageBox.Show(
            $"Se produjo un error no controlado.\n\nDetalle: {e.Exception.Message}",
            "Error de aplicacion",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Error fatal en la aplicacion");
            MessageBox.Show(
                $"Se produjo un error fatal.\n\nDetalle: {ex.Message}",
                "Error fatal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Error en tarea asincrona no observada");
        MessageBox.Show(
            $"Se produjo un error en una tarea asincrona.\n\nDetalle: {e.Exception.GetBaseException().Message}",
            "Error asincrono",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.SetObserved();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: true));

        services.AddSingleton<IDialogService, MessageBoxDialogService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<BusinessSettingsService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<IAuthenticationService, AuthenticationService>();
        services.AddTransient<IInventoryRepository, InventoryRepository>();
        services.AddTransient<IInventoryService, InventoryService>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<ProductRepository>();
        services.AddTransient<IReportsRepository, ReportsRepository>();
        services.AddTransient<ReportsRepository>();
        services.AddTransient<ISalesNoteRepository, SalesNoteRepository>();

        services.AddTransient<AuthRepository>();
        services.AddTransient<DashboardRepository>();
        services.AddTransient<CategoryRepository>();
        services.AddTransient<WarehouseRepository>();
        services.AddTransient<KitRepository>();
        services.AddTransient<ClientRepository>();
        services.AddTransient<UserRepository>();
        services.AddTransient<SalesBetaRepository>();
        services.AddTransient<ProveedorRepository>();
        services.AddTransient<OrdenCompraRepository>();
        services.AddTransient<QuotationRepository>();
        services.AddTransient<CajaRepository>();
        services.AddTransient<ReportsRepository>();
        services.AddTransient<DatabaseBackupService>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DatabaseBackupService>>();
            return new DatabaseBackupService(logger, DbConnectionFactory.GetConnectionString());
        });
        services.AddTransient<AuditRepository>();

        services.AddTransient<SalesNotesViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<KitsViewModel>();
        services.AddTransient<WarehousesViewModel>();
        services.AddTransient<ClientsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<ProvidersViewModel>();
        services.AddTransient<PurchasesViewModel>();
        services.AddTransient<QuotationViewModel>();
        services.AddTransient<SalesBetaViewModel>();
        services.AddTransient<BusinessSettingsViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<SaleNoteEditViewModel>();

        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SaleNoteEditWindow>();
    }
}
