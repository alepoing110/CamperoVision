using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class ClientRepository
{
    private readonly IAuditService _auditService;

    public ClientRepository(IAuditService auditService)
    {
        _auditService = auditService;
    }
    public async Task<List<ClientListItem>> GetAllAsync(string search = "")
    {
        const string sql = @"
SELECT
    id_cliente,
    nombre,
    COALESCE(ci_nit, '') AS ci_nit,
    COALESCE(telefono, '') AS telefono,
    COALESCE(email, '') AS email,
    COALESCE(direccion, '') AS direccion,
    activo
FROM clientes
WHERE (@search = '' OR nombre LIKE CONCAT('%', @search, '%') OR ci_nit LIKE CONCAT('%', @search, '%'))
ORDER BY nombre;";

        var items = new List<ClientListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new ClientListItem
            {
                IdCliente = reader.GetInt32("id_cliente"),
                Nombre = reader.GetString("nombre"),
                CiNit = reader.GetString("ci_nit"),
                Telefono = reader.GetString("telefono"),
                Email = reader.GetString("email"),
                Direccion = reader.GetString("direccion"),
                Activo = reader.GetBoolean("activo")
            });
        }

        return items;
    }

    public async Task<List<ClientOption>> GetOptionsAsync()
    {
        const string sql = @"SELECT id_cliente, nombre, COALESCE(ci_nit, '') AS ci_nit FROM clientes WHERE activo = 1 ORDER BY nombre;";
        var items = new List<ClientOption>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ClientOption
            {
                IdCliente = reader.GetInt32("id_cliente"),
                Nombre = reader.GetString("nombre"),
                CiNit = reader.GetString("ci_nit")
            });
        }

        return items;
    }

    public async Task SaveAsync(ClientUpsertModel model)
    {
        bool isUpdate = model.IdCliente != 0;
        object? datosAnteriores = null;

        if (isUpdate)
        {
            await using var conn = DbConnectionFactory.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT nombre, ci_nit, telefono, email, direccion, activo FROM clientes WHERE id_cliente = @id;", conn);
            cmd.Parameters.AddWithValue("@id", model.IdCliente);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                datosAnteriores = new
                {
                    nombre = reader.GetString("nombre"),
                    ci_nit = reader.GetStringSafe("ci_nit"),
                    telefono = reader.GetStringSafe("telefono"),
                    email = reader.GetStringSafe("email"),
                    direccion = reader.GetStringSafe("direccion"),
                    activo = reader.GetBoolean("activo")
                };
            }
        }

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        string sql = isUpdate
            ? @"UPDATE clientes
                SET nombre=@nombre, ci_nit=@ci_nit, telefono=@telefono, email=@email, direccion=@direccion, activo=@activo
                WHERE id_cliente=@id_cliente;"
            : @"INSERT INTO clientes (nombre, ci_nit, telefono, email, direccion, activo)
                VALUES (@nombre, @ci_nit, @telefono, @email, @direccion, @activo);";

        await using var command = new MySqlCommand(sql, connection);
        if (isUpdate)
        {
            command.Parameters.AddWithValue("@id_cliente", model.IdCliente);
        }

        command.Parameters.AddWithValue("@nombre", model.Nombre);
        command.Parameters.AddWithValue("@ci_nit", string.IsNullOrWhiteSpace(model.CiNit) ? DBNull.Value : model.CiNit);
        command.Parameters.AddWithValue("@telefono", string.IsNullOrWhiteSpace(model.Telefono) ? DBNull.Value : model.Telefono);
        command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(model.Email) ? DBNull.Value : model.Email);
        command.Parameters.AddWithValue("@direccion", string.IsNullOrWhiteSpace(model.Direccion) ? DBNull.Value : model.Direccion);
        command.Parameters.AddWithValue("@activo", model.Activo);

        await command.ExecuteNonQueryAsync();

        var datosNuevos = new
        {
            nombre = model.Nombre,
            ci_nit = model.CiNit,
            telefono = model.Telefono,
            email = model.Email,
            direccion = model.Direccion,
            activo = model.Activo
        };

        await _auditService.LogAsync(
            "clientes",
            isUpdate ? "UPDATE" : "CREATE",
            model.IdCliente,
            isUpdate ? $"Cliente actualizado: {model.Nombre}" : $"Cliente creado: {model.Nombre}",
            datosAnteriores,
            datosNuevos);
    }
}
