using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class ProductRepository : IProductRepository
{
    private readonly IAuditService _auditService;

    public ProductRepository(IAuditService auditService)
    {
        _auditService = auditService;
    }
    public sealed class ProductDuplicateCheckResult
    {
        public bool IdConflict { get; set; }
        public bool CodigoConflict { get; set; }
        public bool CodigoBarrasConflict { get; set; }
        public bool NombreConflict { get; set; }
    }

    public async Task<List<ProductListItem>> GetAllAsync(string search = "", int page = 1, int pageSize = 100)
    {
        const string sql = @"
SELECT
    p.id_producto,
    p.codigo,
    COALESCE(p.codigo_barras, '') AS codigo_barras,
    p.nombre,
    c.nombre AS categoria,
    p.precio_compra,
    p.precio_venta,
    p.stock_minimo,
    p.activo
FROM productos p
INNER JOIN categorias c ON c.id_categoria = p.id_categoria
WHERE (
    @search = ''
    OR COALESCE(p.codigo_barras, '') LIKE CONCAT('%', @search, '%')
    OR p.codigo LIKE CONCAT('%', @search, '%')
    OR CAST(p.id_producto AS CHAR) = @search
    OR p.nombre LIKE CONCAT('%', @search, '%')
)
ORDER BY
    CASE
        WHEN @search <> '' AND COALESCE(p.codigo_barras, '') = @search THEN 0
        WHEN @search <> '' AND p.codigo = @search THEN 1
        WHEN @search <> '' AND CAST(p.id_producto AS CHAR) = @search THEN 2
        WHEN @search <> '' AND p.nombre LIKE CONCAT(@search, '%') THEN 3
        WHEN @search <> '' AND p.nombre LIKE CONCAT('%', @search, '%') THEN 4
        WHEN @search <> '' AND COALESCE(p.codigo_barras, '') LIKE CONCAT('%', @search, '%') THEN 5
        WHEN @search <> '' AND p.codigo LIKE CONCAT('%', @search, '%') THEN 6
        ELSE 7
    END,
    p.nombre
LIMIT @offset, @pageSize;";

        var items = new List<ProductListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("@pageSize", pageSize);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new ProductListItem
            {
                IdProducto = reader.GetInt32("id_producto"),
                Codigo = reader.GetString("codigo"),
                CodigoBarras = reader.GetString("codigo_barras"),
                Nombre = reader.GetString("nombre"),
                Categoria = reader.GetString("categoria"),
                PrecioCompra = reader.GetDecimal("precio_compra"),
                PrecioVenta = reader.GetDecimal("precio_venta"),
                StockMinimo = reader.GetInt32("stock_minimo"),
                Activo = reader.GetBoolean("activo")
            });
        }

        return items;
    }

    public async Task<int> GetTotalCountAsync(string search = "")
    {
        const string sql = @"
SELECT COUNT(*)
FROM productos p
INNER JOIN categorias c ON c.id_categoria = p.id_categoria
WHERE (
    @search = ''
    OR COALESCE(p.codigo_barras, '') LIKE CONCAT('%', @search, '%')
    OR p.codigo LIKE CONCAT('%', @search, '%')
    OR CAST(p.id_producto AS CHAR) = @search
    OR p.nombre LIKE CONCAT('%', @search, '%')
);";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<List<ProductListItem>> GetByCategoryAsync(int idCategoria)
    {
        const string sql = @"
SELECT
    p.id_producto,
    p.codigo,
    COALESCE(p.codigo_barras, '') AS codigo_barras,
    p.nombre,
    c.nombre AS categoria,
    p.precio_compra,
    p.precio_venta,
    p.stock_minimo,
    p.activo
FROM productos p
INNER JOIN categorias c ON c.id_categoria = p.id_categoria
WHERE p.id_categoria = @id_categoria
ORDER BY p.nombre;";

        var items = new List<ProductListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_categoria", idCategoria);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new ProductListItem
            {
                IdProducto = reader.GetInt32("id_producto"),
                Codigo = reader.GetString("codigo"),
                CodigoBarras = reader.GetString("codigo_barras"),
                Nombre = reader.GetString("nombre"),
                Categoria = reader.GetString("categoria"),
                PrecioCompra = reader.GetDecimal("precio_compra"),
                PrecioVenta = reader.GetDecimal("precio_venta"),
                StockMinimo = reader.GetInt32("stock_minimo"),
                Activo = reader.GetBoolean("activo")
            });
        }

        return items;
    }

    public async Task<ProductDuplicateCheckResult> CheckDuplicatesAsync(ProductUpsertModel model)
    {
        const string sql = @"
SELECT
    EXISTS(
        SELECT 1
        FROM productos
        WHERE id_producto = @id_producto
          AND @id_producto <> 0
    ) AS id_exists,
    EXISTS(
        SELECT 1
        FROM productos
        WHERE codigo = @codigo
          AND (@id_producto = 0 OR id_producto <> @id_producto)
    ) AS codigo_exists,
    EXISTS(
        SELECT 1
        FROM productos
        WHERE @codigo_barras <> ''
          AND COALESCE(codigo_barras, '') = @codigo_barras
          AND (@id_producto = 0 OR id_producto <> @id_producto)
    ) AS codigo_barras_exists,
    EXISTS(
        SELECT 1
        FROM productos
        WHERE nombre = @nombre
          AND (@id_producto = 0 OR id_producto <> @id_producto)
    ) AS nombre_exists;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_producto", model.IdProducto);
        command.Parameters.AddWithValue("@codigo", model.Codigo.Trim());
        command.Parameters.AddWithValue("@codigo_barras", model.CodigoBarras.Trim());
        command.Parameters.AddWithValue("@nombre", model.Nombre.Trim());
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return new ProductDuplicateCheckResult();
        }

        return new ProductDuplicateCheckResult
        {
            IdConflict = model.IdProducto != 0 && !reader.GetBoolean("id_exists"),
            CodigoConflict = reader.GetBoolean("codigo_exists"),
            CodigoBarrasConflict = reader.GetBoolean("codigo_barras_exists"),
            NombreConflict = reader.GetBoolean("nombre_exists")
        };
    }

    public async Task<bool> BarcodeExistsAsync(string codigoBarras, int excludeProductId = 0)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM productos
    WHERE COALESCE(codigo_barras, '') = @codigo_barras
      AND (@exclude_id = 0 OR id_producto <> @exclude_id)
);";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@codigo_barras", codigoBarras.Trim());
        command.Parameters.AddWithValue("@exclude_id", excludeProductId);
        object? result = await command.ExecuteScalarAsync();
        return result is not null && Convert.ToBoolean(result);
    }

    public async Task<List<CategoryItem>> GetCategoriesAsync()
    {
        const string sql = "SELECT id_categoria, nombre FROM categorias WHERE activo = 1 ORDER BY nombre;";
        var items = new List<CategoryItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new CategoryItem
            {
                IdCategoria = reader.GetInt32("id_categoria"),
                Nombre = reader.GetString("nombre")
            });
        }

        return items;
    }

    public async Task SaveAsync(ProductUpsertModel model)
    {
        bool isUpdate = model.IdProducto != 0;
        object? datosAnteriores = null;

        if (isUpdate)
        {
            await using var conn = DbConnectionFactory.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT id_categoria, codigo, codigo_barras, nombre, descripcion, precio_compra, precio_venta, unidad_medida, stock_minimo, activo FROM productos WHERE id_producto = @id;", conn);
            cmd.Parameters.AddWithValue("@id", model.IdProducto);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                datosAnteriores = new
                {
                    id_categoria = reader.GetInt32("id_categoria"),
                    codigo = reader.GetString("codigo"),
                    codigo_barras = reader.GetStringSafe("codigo_barras"),
                    nombre = reader.GetString("nombre"),
                    descripcion = reader.GetStringSafe("descripcion"),
                    precio_compra = reader.GetDecimal("precio_compra"),
                    precio_venta = reader.GetDecimal("precio_venta"),
                    unidad_medida = reader.GetStringSafe("unidad_medida"),
                    stock_minimo = reader.GetInt32("stock_minimo"),
                    activo = reader.GetBoolean("activo")
                };
            }
        }

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        string sql = isUpdate
            ? @"UPDATE productos
                SET id_categoria=@id_categoria, codigo=@codigo, codigo_barras=@codigo_barras, nombre=@nombre, descripcion=@descripcion,
                    precio_compra=@precio_compra, precio_venta=@precio_venta, unidad_medida=@unidad_medida,
                    stock_minimo=@stock_minimo, activo=@activo
                WHERE id_producto=@id_producto;"
            : @"INSERT INTO productos (id_categoria, codigo, codigo_barras, nombre, descripcion, precio_compra, precio_venta, unidad_medida, stock_minimo, activo)
                VALUES (@id_categoria, @codigo, @codigo_barras, @nombre, @descripcion, @precio_compra, @precio_venta, @unidad_medida, @stock_minimo, @activo);";

        await using var command = new MySqlCommand(sql, connection);
        if (isUpdate)
        {
            command.Parameters.AddWithValue("@id_producto", model.IdProducto);
        }

        command.Parameters.AddWithValue("@id_categoria", model.IdCategoria);
        command.Parameters.AddWithValue("@codigo", model.Codigo);
        command.Parameters.AddWithValue("@codigo_barras", string.IsNullOrWhiteSpace(model.CodigoBarras) ? DBNull.Value : model.CodigoBarras.Trim());
        command.Parameters.AddWithValue("@nombre", model.Nombre);
        command.Parameters.AddWithValue("@descripcion", string.IsNullOrWhiteSpace(model.Descripcion) ? DBNull.Value : model.Descripcion);
        command.Parameters.AddWithValue("@precio_compra", model.PrecioCompra);
        command.Parameters.AddWithValue("@precio_venta", model.PrecioVenta);
        command.Parameters.AddWithValue("@unidad_medida", model.UnidadMedida);
        command.Parameters.AddWithValue("@stock_minimo", model.StockMinimo);
        command.Parameters.AddWithValue("@activo", model.Activo);

        await command.ExecuteNonQueryAsync();

        var datosNuevos = new
        {
            id_categoria = model.IdCategoria,
            codigo = model.Codigo,
            codigo_barras = model.CodigoBarras,
            nombre = model.Nombre,
            descripcion = model.Descripcion,
            precio_compra = model.PrecioCompra,
            precio_venta = model.PrecioVenta,
            unidad_medida = model.UnidadMedida,
            stock_minimo = model.StockMinimo,
            activo = model.Activo
        };

        await _auditService.LogAsync(
            "productos",
            isUpdate ? "UPDATE" : "CREATE",
            model.IdProducto,
            isUpdate ? $"Producto actualizado: {model.Nombre}" : $"Producto creado: {model.Nombre}",
            datosAnteriores,
            datosNuevos);
    }
}
