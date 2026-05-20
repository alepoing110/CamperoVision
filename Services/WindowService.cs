using System.Windows;
using CamperoDesktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CamperoDesktop.Services;

public class WindowService : IWindowService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionService _sessionService;

    public WindowService(IServiceProvider serviceProvider, ISessionService sessionService)
    {
        _serviceProvider = serviceProvider;
        _sessionService = sessionService;
    }

    public async Task ShowMainWindowAsync()
    {
        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        if (mainWindow.DataContext is MainViewModel mainViewModel)
        {
            await mainViewModel.InitializeAsync();
        }

        mainWindow.Show();
        CloseCurrentWindow<LoginWindow>();
    }

    public void ShowLoginWindow()
    {
        LoginWindow loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        loginWindow.Show();
        CloseCurrentWindow<MainWindow>();
    }

    public void Logout()
    {
        _sessionService.ClearSession();
        ShowLoginWindow();
    }

    private static void CloseCurrentWindow<TWindow>() where TWindow : Window
    {
        TWindow? window = Application.Current.Windows.OfType<TWindow>().FirstOrDefault(w => w.IsActive)
            ?? Application.Current.Windows.OfType<TWindow>().FirstOrDefault();

        window?.Close();
    }
}
