using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class CategoryRepository
{
    private readonly IAuditService _auditService;

    public CategoryRepository(IAuditService auditService)
    {
        _auditService = auditService;
    }
    public async Task<List<CategoryListItem>> GetAllAsync(string search = "")
    {
        const string sql = @"
SELECT id_categoria,
       nombre,
       COALESCE(descripcion, '') AS descripcion,
       activo
FROM categorias
WHERE @search = ''
   OR nombre LIKE CONCAT('%', @search, '%')
   OR COALESCE(descripcion, '') LIKE CONCAT('%', @search, '%')
ORDER BY nombre;";

        var items = new List<CategoryListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new CategoryListItem
            {
                IdCategoria = reader.GetInt32("id_categoria"),
                Nombre = reader.GetString("nombre"),
                Descripcion = reader.GetString("descripcion"),
                Activo = reader.GetBoolean("activo")
            });
        }

        return items;
    }

    public async Task SaveAsync(CategoryUpsertModel model)
    {
        bool isUpdate = model.IdCategoria != 0;
        object? datosAnteriores = null;

        if (isUpdate)
        {
            await using var conn = DbConnectionFactory.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT nombre, descripcion, activo FROM categorias WHERE id_categoria = @id;", conn);
            cmd.Parameters.AddWithValue("@id", model.IdCategoria);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                datosAnteriores = new
                {
                    nombre = reader.GetString("nombre"),
                    descripcion = reader.GetStringSafe("descripcion"),
                    activo = reader.GetBoolean("activo")
                };
            }
        }

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        string sql = isUpdate
            ? @"UPDATE categorias
                SET nombre=@nombre, descripcion=@descripcion, activo=@activo
                WHERE id_categoria=@id_categoria;"
            : @"INSERT INTO categorias (nombre, descripcion, activo)
                VALUES (@nombre, @descripcion, @activo);";

        await using var command = new MySqlCommand(sql, connection);
        if (isUpdate)
        {
            command.Parameters.AddWithValue("@id_categoria", model.IdCategoria);
        }

        command.Parameters.AddWithValue("@nombre", model.Nombre.Trim());
        command.Parameters.AddWithValue("@descripcion", string.IsNullOrWhiteSpace(model.Descripcion) ? DBNull.Value : model.Descripcion.Trim());
        command.Parameters.AddWithValue("@activo", model.Activo);
        await command.ExecuteNonQueryAsync();

        var datosNuevos = new
        {
            nombre = model.Nombre.Trim(),
            descripcion = model.Descripcion?.Trim(),
            activo = model.Activo
        };

        await _auditService.LogAsync(
            "categorias",
            isUpdate ? "UPDATE" : "CREATE",
            model.IdCategoria,
            isUpdate ? $"Categoria actualizada: {model.Nombre}" : $"Categoria creada: {model.Nombre}",
            datosAnteriores,
            datosNuevos);
    }

    public async Task<bool> HasProductsAsync(int idCategoria)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM productos WHERE id_categoria = @id_categoria LIMIT 1);";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_categoria", idCategoria);

        object? result = await command.ExecuteScalarAsync();
        return result is not null && Convert.ToBoolean(result);
    }

    public async Task DeleteAsync(int idCategoria)
    {
        await using var conn = DbConnectionFactory.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT nombre FROM categorias WHERE id_categoria = @id;", conn);
        cmd.Parameters.AddWithValue("@id", idCategoria);
        var nombre = (await cmd.ExecuteScalarAsync())?.ToString() ?? idCategoria.ToString();

        const string sql = "DELETE FROM categorias WHERE id_categoria = @id_categoria;";
        await using var command = new MySqlCommand(sql, conn);
        command.Parameters.AddWithValue("@id_categoria", idCategoria);
        await command.ExecuteNonQueryAsync();

        await _auditService.LogAsync("categorias", "DELETE", idCategoria, $"Categoria eliminada: {nombre}");
    }
}
