using CamperoDesktop.Data;
using CamperoDesktop.Models;

namespace CamperoDesktop.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly AuthRepository _authRepository;
    private readonly ISessionService _sessionService;

    public AuthenticationService(AuthRepository authRepository, ISessionService sessionService)
    {
        _authRepository = authRepository;
        _sessionService = sessionService;
    }

    public async Task<UserSession?> LoginAsync(string username, string password)
    {
        UserSession? session = await _authRepository.LoginAsync(username, password);
        if (session is not null)
        {
            _sessionService.StartSession(session);
        }

        return session;
    }
}
