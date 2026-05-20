using CamperoDesktop.Models;

namespace CamperoDesktop.Services;

public interface IAuthenticationService
{
    Task<UserSession?> LoginAsync(string username, string password);
}
