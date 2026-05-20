using CamperoDesktop.Models;

namespace CamperoDesktop.Services;

public class SessionService : ISessionService
{
    public UserSession? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public void StartSession(UserSession session)
    {
        CurrentUser = session;
    }

    public void ClearSession()
    {
        CurrentUser = null;
    }
}
