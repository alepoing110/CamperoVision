using System.Configuration;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace CamperoDesktop.Data;

public static class DbConnectionFactory
{
    private static readonly Lazy<IConfigurationRoot> Configuration = new(LoadConfiguration);

    public static string GetConnectionString()
    {
        string? connectionString = Configuration.Value.GetConnectionString("MySqlConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = System.Configuration.ConfigurationManager
                .ConnectionStrings["MySqlConnection"]?.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontro la cadena de conexion 'MySqlConnection' en appsettings.json ni en App.config.");
        }

        return connectionString;
    }

    public static MySqlConnection CreateConnection()
    {
        return new MySqlConnection(GetConnectionString());
    }

    private static IConfigurationRoot LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }
}
