namespace CamperoDesktop.Services;

public interface IWindowService
{
    Task ShowMainWindowAsync();
    void ShowLoginWindow();
    void Logout();
}
