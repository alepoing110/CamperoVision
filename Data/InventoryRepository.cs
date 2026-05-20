using CamperoDesktop.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class InventoryRepository : IInventoryRepository
{
    private readonly KitRepository _kitRepository;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(KitRepository kitRepository, ILogger<InventoryRepository> logger)
    {
        _kitRepository = kitRepository;
        _logger = logger;
    }

    public async Task<List<WarehouseItem>> GetWarehousesAsync()
    {
        const string sql = "SELECT id_almacen, nombre FROM almacenes WHERE activo = 1 ORDER BY nombre;";
        var items = new List<WarehouseItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new WarehouseItem { IdAlmacen = reader.GetInt32("id_almacen"), Nombre = reader.GetString("nombre") });
        }
        return items;
    }

    public async Task<List<ProductOption>> GetProductsAsync(int? warehouseId = null, string search = "", bool includeKits = false)
    {
        const string sql = @"
SELECT p.id_producto,
       p.codigo,
       COALESCE(p.codigo_barras, '') AS codigo_barras,
       p.nombre,
       COALESCE(p.descripcion, '') AS descripcion,
       COALESCE(p.unidad_medida, 'unidad') AS unidad_medida,
       p.precio_venta,
       COALESCE(i.cantidad,0) AS stock_disponible
FROM productos p
LEFT JOIN inventario i ON i.id_producto = p.id_producto AND (@warehouseId IS NULL OR i.id_almacen = @warehouseId)
WHERE p.activo = 1
  AND (
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
    p.nombre;";

        var items = new List<ProductOption>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@warehouseId", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ProductOption
            {
                IdProducto = reader.GetInt32("id_producto"),
                IdKit = null,
                IsKit = false,
                Codigo = reader.GetString("codigo"),
                CodigoBarras = reader.GetString("codigo_barras"),
                Nombre = reader.GetString("nombre"),
                Descripcion = reader.GetString("descripcion"),
                UnidadMedida = reader.GetString("unidad_medida"),
                PrecioVenta = reader.GetDecimal("precio_venta"),
                StockDisponible = reader.GetInt32("stock_disponible")
            });
        }

        if (includeKits)
        {
            items.AddRange(await _kitRepository.GetOptionsAsync(warehouseId, search ?? string.Empty));
        }

        return items;
    }

    public async Task<List<InventoryListItem>> GetAllAsync(int? warehouseId = null, string search = "", int page = 1, int pageSize = 200)
    {
        const string sql = @"
SELECT i.id_inventario,
       i.id_producto,
       i.id_almacen,
       a.nombre AS almacen,
       p.codigo AS codigo_producto,
       COALESCE(p.codigo_barras, '') AS codigo_barras,
       p.nombre AS producto,
       i.cantidad,
       p.stock_minimo,
       i.fecha_actualizacion
FROM inventario i
INNER JOIN almacenes a ON a.id_almacen = i.id_almacen
INNER JOIN productos p ON p.id_producto = i.id_producto
WHERE (@warehouseId IS NULL OR i.id_almacen = @warehouseId)
  AND (
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
    a.nombre,
    p.nombre
LIMIT @offset, @pageSize;";

        var items = new List<InventoryListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@warehouseId", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        command.Parameters.AddWithValue("@offset", Math.Max(0, (page - 1) * pageSize));
        command.Parameters.AddWithValue("@pageSize", pageSize);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new InventoryListItem
            {
                IdInventario = reader.GetInt32("id_inventario"),
                IdProducto = reader.GetInt32("id_producto"),
                IdAlmacen = reader.GetInt32("id_almacen"),
                Almacen = reader.GetString("almacen"),
                CodigoProducto = reader.GetString("codigo_producto"),
                CodigoBarras = reader.GetString("codigo_barras"),
                Producto = reader.GetString("producto"),
                Cantidad = reader.GetInt32("cantidad"),
                StockMinimo = reader.GetInt32("stock_minimo"),
                FechaActualizacion = reader.GetDateTime("fecha_actualizacion")
            });
        }
        return items;
    }

    public async Task<int> GetCurrentStockAsync(int productId, int warehouseId)
    {
        const string sql = @"
SELECT COALESCE(cantidad, 0)
FROM inventario
WHERE id_producto = @id_producto AND id_almacen = @id_almacen
LIMIT 1;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_producto", productId);
        command.Parameters.AddWithValue("@id_almacen", warehouseId);
        object? result = await command.ExecuteScalarAsync();
        return result is null ? 0 : Convert.ToInt32(result);
    }

    public async Task<List<InventoryMovementItem>> GetKardexAsync(int? warehouseId = null, int? productId = null, int limit = 100)
    {
        const string sql = @"
SELECT m.id_movimiento,
       m.fecha,
       m.tipo,
       p.codigo,
       COALESCE(p.codigo_barras, '') AS codigo_barras,
       p.nombre AS producto,
       a.nombre AS almacen,
       m.cantidad,
       COALESCE(m.motivo, '') AS motivo,
       u.nombre AS usuario
FROM movimientos_stock m
INNER JOIN productos p ON p.id_producto = m.id_producto
INNER JOIN almacenes a ON a.id_almacen = m.id_almacen
INNER JOIN usuarios u ON u.id_usuario = m.id_usuario
WHERE (@warehouseId IS NULL OR m.id_almacen = @warehouseId)
  AND (@productId IS NULL OR m.id_producto = @productId)
ORDER BY m.fecha DESC, m.id_movimiento DESC
LIMIT @limit;";

        var items = new List<InventoryMovementItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@warehouseId", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@productId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@limit", limit);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new InventoryMovementItem
            {
                IdMovimiento = reader.GetInt32("id_movimiento"),
                Fecha = reader.GetDateTime("fecha"),
                Tipo = reader.GetString("tipo"),
                Codigo = reader.GetString("codigo"),
                CodigoBarras = reader.GetString("codigo_barras"),
                Producto = reader.GetString("producto"),
                Almacen = reader.GetString("almacen"),
                Cantidad = reader.GetInt32("cantidad"),
                Motivo = reader.GetString("motivo"),
                Usuario = reader.GetString("usuario")
            });
        }
        return items;
    }

    public async Task RegisterMovementAsync(InventoryMovementRequest request)
    {
        _logger.LogDebug("Registrando movimiento de inventario: Producto {ProductId}, Almacen {WarehouseId}, Tipo {Tipo}, Cantidad {Cantidad}", request.IdProducto, request.IdAlmacen, request.Tipo, request.Cantidad);
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        const string selectSql = "SELECT cantidad FROM inventario WHERE id_producto=@id_producto AND id_almacen=@id_almacen LIMIT 1 FOR UPDATE;";
        await using var selectCommand = new MySqlCommand(selectSql, connection, transaction);
        selectCommand.Parameters.AddWithValue("@id_producto", request.IdProducto);
        selectCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
        object? currentValue = await selectCommand.ExecuteScalarAsync();
        int currentQuantity = currentValue is null ? 0 : Convert.ToInt32(currentValue);

        int signedQuantity = request.Tipo == "salida" ? -request.Cantidad : request.Cantidad;
        int newQuantity = currentQuantity + signedQuantity;
        if (newQuantity < 0)
        {
            throw new InvalidOperationException("El movimiento deja inventario negativo.");
        }

        if (currentValue is null)
        {
            const string insertInventory = "INSERT INTO inventario (id_producto, id_almacen, cantidad) VALUES (@id_producto, @id_almacen, @cantidad);";
            await using var insertCommand = new MySqlCommand(insertInventory, connection, transaction);
            insertCommand.Parameters.AddWithValue("@id_producto", request.IdProducto);
            insertCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
            insertCommand.Parameters.AddWithValue("@cantidad", newQuantity);
            await insertCommand.ExecuteNonQueryAsync();
        }
        else
        {
            const string updateInventory = "UPDATE inventario SET cantidad=@cantidad WHERE id_producto=@id_producto AND id_almacen=@id_almacen;";
            await using var updateCommand = new MySqlCommand(updateInventory, connection, transaction);
            updateCommand.Parameters.AddWithValue("@cantidad", newQuantity);
            updateCommand.Parameters.AddWithValue("@id_producto", request.IdProducto);
            updateCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
            await updateCommand.ExecuteNonQueryAsync();
        }

        const string movementSql = @"INSERT INTO movimientos_stock (id_producto, id_almacen, id_usuario, tipo, cantidad, motivo)
                                     VALUES (@id_producto, @id_almacen, @id_usuario, @tipo, @cantidad, @motivo);";
        await using var movementCommand = new MySqlCommand(movementSql, connection, transaction);
        movementCommand.Parameters.AddWithValue("@id_producto", request.IdProducto);
        movementCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
        movementCommand.Parameters.AddWithValue("@id_usuario", request.IdUsuario);
        movementCommand.Parameters.AddWithValue("@tipo", request.Tipo);
        movementCommand.Parameters.AddWithValue("@cantidad", request.Cantidad);
        movementCommand.Parameters.AddWithValue("@motivo", BuildReason(request));
        await movementCommand.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
    }

    public async Task AdjustStockAsync(InventoryAdjustmentRequest request)
    {
        await RegisterMovementAsync(new InventoryMovementRequest
        {
            IdProducto = request.IdProducto,
            IdAlmacen = request.IdAlmacen,
            IdUsuario = request.IdUsuario,
            Cantidad = Math.Abs(request.Cantidad),
            Precio = 0,
            Tipo = request.Cantidad >= 0 ? "entrada" : "salida",
            Motivo = request.Motivo
        });
    }

    private static string BuildReason(InventoryMovementRequest request)
    {
        string priceText = request.Precio > 0 ? $" | Precio: {request.Precio:N2}" : string.Empty;
        return string.IsNullOrWhiteSpace(request.Motivo)
            ? $"Movimiento {request.Tipo}{priceText}"
            : $"{request.Motivo}{priceText}";
    }
}
