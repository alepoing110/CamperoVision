using CamperoDesktop.Models;

namespace CamperoDesktop.Services;

public interface ISessionService
{
    UserSession? CurrentUser { get; }
    bool IsAuthenticated { get; }
    void StartSession(UserSession session);
    void ClearSession();
}
