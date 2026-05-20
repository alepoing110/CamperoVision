using CamperoDesktop.Models;
using MySqlConnector;
using System.Data;

namespace CamperoDesktop.Data;

public class ProveedorRepository
{
    public async Task<List<ProveedorOption>> GetOptionsAsync()
    {
        const string sql = @"
SELECT id_proveedor, nombre, COALESCE(nit, '') AS nit
FROM proveedores
WHERE activo = 1
ORDER BY nombre";

        var items = new List<ProveedorOption>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ProveedorOption
            {
                IdProveedor = reader.GetInt32("id_proveedor"),
                Nombre = reader.GetString("nombre"),
                Nit = reader.GetString("nit")
            });
        }

        return items;
    }

    public async Task<List<ProveedorListItem>> GetAllAsync()
    {
        const string sql = @"
SELECT id_proveedor, nombre, nit, telefono, email, direccion, activo
FROM proveedores
ORDER BY nombre";

        var items = new List<ProveedorListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ProveedorListItem
            {
                IdProveedor = reader.GetInt32("id_proveedor"),
                Nombre = reader.GetString("nombre"),
                Nit = reader.IsDBNull("nit") ? "" : reader.GetString("nit"),
                Telefono = reader.IsDBNull("telefono") ? "" : reader.GetString("telefono"),
                Email = reader.IsDBNull("email") ? "" : reader.GetString("email"),
                Direccion = reader.IsDBNull("direccion") ? "" : reader.GetString("direccion"),
                Activo = reader.GetBoolean("activo")
            });
        }
        return items;
    }

    public async Task<ProveedorUpsertModel?> GetByIdAsync(int id)
    {
        const string sql = @"
SELECT id_proveedor, nombre, nit, telefono, email, direccion, activo
FROM proveedores
WHERE id_proveedor = @id";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProveedorUpsertModel
            {
                IdProveedor = reader.GetInt32("id_proveedor"),
                Nombre = reader.GetString("nombre"),
                Nit = reader.IsDBNull("nit") ? "" : reader.GetString("nit"),
                Telefono = reader.IsDBNull("telefono") ? "" : reader.GetString("telefono"),
                Email = reader.IsDBNull("email") ? "" : reader.GetString("email"),
                Direccion = reader.IsDBNull("direccion") ? "" : reader.GetString("direccion"),
                Activo = reader.GetBoolean("activo")
            };
        }
        return null;
    }

    public async Task<int> CreateAsync(ProveedorUpsertModel model)
    {
        const string sql = @"
INSERT INTO proveedores (nombre, nit, telefono, email, direccion, activo)
VALUES (@nombre, @nit, @telefono, @email, @direccion, @activo)";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@nombre", MySqlDbType.VarChar).Value = model.Nombre ?? (object)DBNull.Value;
        command.Parameters.Add("@nit", MySqlDbType.VarChar).Value = model.Nit ?? (object)DBNull.Value;
        command.Parameters.Add("@telefono", MySqlDbType.VarChar).Value = model.Telefono ?? (object)DBNull.Value;
        command.Parameters.Add("@email", MySqlDbType.VarChar).Value = model.Email ?? (object)DBNull.Value;
        command.Parameters.Add("@direccion", MySqlDbType.Text).Value = model.Direccion ?? (object)DBNull.Value;
        command.Parameters.Add("@activo", MySqlDbType.Bit).Value = model.Activo;
        await command.ExecuteNonQueryAsync();
        return (int)command.LastInsertedId;
    }

    public async Task UpdateAsync(ProveedorUpsertModel model)
    {
        const string sql = @"
UPDATE proveedores 
SET nombre = @nombre, nit = @nit, telefono = @telefono, email = @email, direccion = @direccion, activo = @activo
WHERE id_proveedor = @id";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@id", MySqlDbType.Int32).Value = model.IdProveedor;
        command.Parameters.Add("@nombre", MySqlDbType.VarChar).Value = model.Nombre ?? (object)DBNull.Value;
        command.Parameters.Add("@nit", MySqlDbType.VarChar).Value = model.Nit ?? (object)DBNull.Value;
        command.Parameters.Add("@telefono", MySqlDbType.VarChar).Value = model.Telefono ?? (object)DBNull.Value;
        command.Parameters.Add("@email", MySqlDbType.VarChar).Value = model.Email ?? (object)DBNull.Value;
        command.Parameters.Add("@direccion", MySqlDbType.Text).Value = model.Direccion ?? (object)DBNull.Value;
        command.Parameters.Add("@activo", MySqlDbType.Bit).Value = model.Activo;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM proveedores WHERE id_proveedor = @id";
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
        await command.ExecuteNonQueryAsync();
    }
}
