using System.IO;
using Microsoft.Extensions.Configuration;

namespace CamperoDesktop.Services;

public static class BusinessBranding
{
    private static Lazy<IConfigurationRoot> _configuration = new(LoadConfiguration);

    public static string BusinessName => GetSetting("Business:Name", "Campero Vision");
    public static string Address => GetSetting("Business:Address", "Aroma Brasil Ex Terminal Galeria 68 Segundo Piso");
    public static string Phone => GetSetting("Business:Phone", "76130968");
    public static string Nit => GetSetting("Business:Nit", string.Empty);
    public static string Email => GetSetting("Business:Email", string.Empty);
    public static string Branch => GetSetting("Business:Branch", "Casa Matriz");
    public static string FooterMessage => GetSetting("Business:FooterMessage", "Gracias por su preferencia.");

    public static string? GetLogoPath()
    {
        string? configuredPath = _configuration.Value["Business:LogoPath"];
        List<string> candidates = new();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath));

            candidates.Add(Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(Directory.GetCurrentDirectory(), "WpfMysqlStarter", configuredPath));

            candidates.Add(Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        }

        candidates.AddRange(
        [
            Path.Combine(AppContext.BaseDirectory, "assets", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "WpfMysqlStarter", "assets", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "assets", "logo.png")
        ]);

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string GetSetting(string key, string fallback)
    {
        string? value = _configuration.Value[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    public static void Reload()
    {
        _configuration = new Lazy<IConfigurationRoot>(LoadConfiguration);
    }

    private static IConfigurationRoot LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }
}
