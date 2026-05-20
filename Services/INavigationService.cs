using CamperoDesktop.Models;

namespace CamperoDesktop.Services;

public interface INavigationService
{
    event EventHandler<ModuleNavigationState>? Navigated;
    Task NavigateToAsync(AppModule module);
}
