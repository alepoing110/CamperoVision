using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class WarehouseRepository
{
    public async Task<List<WarehouseListItem>> GetAllAsync(string search = "")
    {
        const string sql = @"
SELECT id_almacen,
       nombre,
       COALESCE(direccion, '') AS direccion,
       COALESCE(responsable, '') AS responsable,
       activo
FROM almacenes
WHERE @search = ''
   OR nombre LIKE CONCAT('%', @search, '%')
   OR COALESCE(direccion, '') LIKE CONCAT('%', @search, '%')
   OR COALESCE(responsable, '') LIKE CONCAT('%', @search, '%')
ORDER BY nombre;";

        var items = new List<WarehouseListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new WarehouseListItem
            {
                IdAlmacen = reader.GetInt32("id_almacen"),
                Nombre = reader.GetString("nombre"),
                Direccion = reader.GetString("direccion"),
                Responsable = reader.GetString("responsable"),
                Activo = reader.GetBoolean("activo")
            });
        }

        return items;
    }

    public async Task SaveAsync(WarehouseUpsertModel model)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        string sql = model.IdAlmacen == 0
            ? @"INSERT INTO almacenes (nombre, direccion, responsable, activo)
                VALUES (@nombre, @direccion, @responsable, @activo);"
            : @"UPDATE almacenes
                SET nombre=@nombre, direccion=@direccion, responsable=@responsable, activo=@activo
                WHERE id_almacen=@id_almacen;";

        await using var command = new MySqlCommand(sql, connection);
        if (model.IdAlmacen != 0)
        {
            command.Parameters.AddWithValue("@id_almacen", model.IdAlmacen);
        }

        command.Parameters.AddWithValue("@nombre", model.Nombre.Trim());
        command.Parameters.AddWithValue("@direccion", string.IsNullOrWhiteSpace(model.Direccion) ? DBNull.Value : model.Direccion.Trim());
        command.Parameters.AddWithValue("@responsable", string.IsNullOrWhiteSpace(model.Responsable) ? DBNull.Value : model.Responsable.Trim());
        command.Parameters.AddWithValue("@activo", model.Activo);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeactivateAsync(int idAlmacen)
    {
        const string sql = "UPDATE almacenes SET activo = 0 WHERE id_almacen = @id_almacen;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_almacen", idAlmacen);
        await command.ExecuteNonQueryAsync();
    }
}
