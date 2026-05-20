using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class AuthRepository
{
    public async Task<UserSession?> LoginAsync(string username, string password)
    {
        const string sql = @"
SELECT
    id_usuario,
    nombre,
    usuario,
    rol
FROM usuarios
WHERE usuario = @usuario
  AND password_hash = SHA2(@password, 256)
  AND activo = 1
LIMIT 1;";

        await using MySqlConnection connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using MySqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@usuario", username);
        command.Parameters.AddWithValue("@password", password);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new UserSession
        {
            IdUsuario = reader.GetInt32("id_usuario"),
            Nombre = reader.GetString("nombre"),
            Usuario = reader.GetString("usuario"),
            Rol = reader.GetString("rol")
        };
    }
}
