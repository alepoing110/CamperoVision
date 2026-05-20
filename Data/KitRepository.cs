using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class KitRepository
{
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaReady;

    public Task EnsureSchemaReadyAsync() => EnsureSchemaAsync();

    public async Task<(bool CodigoExiste, bool NombreExiste)> CheckDuplicatesAsync(KitUpsertModel model)
    {
        await EnsureSchemaAsync();

        const string sql = @"
SELECT
    EXISTS(
        SELECT 1
        FROM kits
        WHERE codigo = @codigo
          AND (@id_kit = 0 OR id_kit <> @id_kit)
    ) AS codigo_existe,
    EXISTS(
        SELECT 1
        FROM kits
        WHERE nombre = @nombre
          AND (@id_kit = 0 OR id_kit <> @id_kit)
    ) AS nombre_existe;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_kit", model.IdKit);
        command.Parameters.AddWithValue("@codigo", model.Codigo.Trim());
        command.Parameters.AddWithValue("@nombre", model.Nombre.Trim());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (false, false);
        }

        return (reader.GetBoolean("codigo_existe"), reader.GetBoolean("nombre_existe"));
    }

    public async Task<List<KitListItem>> GetAllAsync(int? warehouseId = null, string search = "")
    {
        await EnsureSchemaAsync();

        const string sql = @"
SELECT
    k.id_kit,
    k.codigo,
    k.nombre,
    COALESCE(k.descripcion, '') AS descripcion,
    k.precio_venta,
    k.activo,
    COUNT(kd.id_producto) AS componentes,
    COALESCE(MIN(FLOOR(COALESCE(i.cantidad, 0) / NULLIF(kd.cantidad, 0))), 0) AS stock_disponible
FROM kits k
LEFT JOIN kit_detalle kd ON kd.id_kit = k.id_kit
LEFT JOIN inventario i ON i.id_producto = kd.id_producto AND (@warehouse_id IS NULL OR i.id_almacen = @warehouse_id)
WHERE
    @search = ''
    OR k.codigo LIKE CONCAT('%', @search, '%')
    OR CAST(k.id_kit AS CHAR) = @search
    OR k.nombre LIKE CONCAT('%', @search, '%')
GROUP BY k.id_kit, k.codigo, k.nombre, k.descripcion, k.precio_venta, k.activo
ORDER BY
    CASE
        WHEN @search <> '' AND k.codigo = @search THEN 0
        WHEN @search <> '' AND CAST(k.id_kit AS CHAR) = @search THEN 1
        WHEN @search <> '' AND k.nombre LIKE CONCAT(@search, '%') THEN 2
        ELSE 3
    END,
    k.nombre;";

        List<KitListItem> items = new();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@warehouse_id", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new KitListItem
            {
                IdKit = reader.GetInt32("id_kit"),
                Codigo = reader.GetString("codigo"),
                Nombre = reader.GetString("nombre"),
                Descripcion = reader.GetString("descripcion"),
                PrecioVenta = reader.GetDecimal("precio_venta"),
                Activo = reader.GetBoolean("activo"),
                Componentes = reader.GetInt32("componentes"),
                StockDisponible = reader.GetInt32("stock_disponible")
            });
        }

        return items;
    }

    public async Task<List<ProductOption>> GetOptionsAsync(int? warehouseId = null, string search = "")
    {
        List<KitListItem> kits = await GetAllAsync(warehouseId, search);
        return kits
            .Where(k => k.Activo && k.Componentes > 0)
            .Select(k => new ProductOption
            {
                IdProducto = 0,
                IdKit = k.IdKit,
                IsKit = true,
                Codigo = k.Codigo,
                CodigoBarras = string.Empty,
                Nombre = k.Nombre,
                Descripcion = string.IsNullOrWhiteSpace(k.Descripcion) ? k.Nombre : k.Descripcion,
                UnidadMedida = "kit",
                PrecioVenta = k.PrecioVenta,
                StockDisponible = k.StockDisponible
            })
            .ToList();
    }

    public async Task<List<KitComponentDraft>> GetProductOptionsForBuilderAsync(int? warehouseId = null, string search = "")
    {
        const string sql = @"
SELECT
    p.id_producto,
    p.codigo,
    p.nombre,
    COALESCE(p.unidad_medida, 'unidad') AS unidad_medida,
    p.precio_compra,
    p.precio_venta,
    COALESCE(i.cantidad, 0) AS stock_disponible
FROM productos p
LEFT JOIN inventario i ON i.id_producto = p.id_producto AND (@warehouse_id IS NULL OR i.id_almacen = @warehouse_id)
WHERE p.activo = 1
  AND (
      @search = ''
      OR p.codigo LIKE CONCAT('%', @search, '%')
      OR CAST(p.id_producto AS CHAR) = @search
      OR p.nombre LIKE CONCAT('%', @search, '%')
      OR COALESCE(p.codigo_barras, '') LIKE CONCAT('%', @search, '%')
  )
ORDER BY
    CASE
        WHEN @search <> '' AND COALESCE(p.codigo_barras, '') = @search THEN 0
        WHEN @search <> '' AND p.codigo = @search THEN 1
        WHEN @search <> '' AND CAST(p.id_producto AS CHAR) = @search THEN 2
        WHEN @search <> '' AND p.nombre LIKE CONCAT(@search, '%') THEN 3
        ELSE 4
    END,
    p.nombre;";

        List<KitComponentDraft> items = new();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@warehouse_id", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@search", search ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new KitComponentDraft
            {
                IdProducto = reader.GetInt32("id_producto"),
                Codigo = reader.GetString("codigo"),
                Nombre = reader.GetString("nombre"),
                UnidadMedida = reader.GetString("unidad_medida"),
                PrecioCompra = reader.GetDecimal("precio_compra"),
                PrecioVenta = reader.GetDecimal("precio_venta"),
                StockDisponible = reader.GetInt32("stock_disponible")
            });
        }

        return items;
    }

    public async Task<KitUpsertModel?> GetByIdAsync(int idKit, int? warehouseId = null)
    {
        await EnsureSchemaAsync();

        const string sql = @"
SELECT id_kit, codigo, nombre, COALESCE(descripcion, '') AS descripcion, precio_venta, activo
FROM kits
WHERE id_kit = @id_kit
LIMIT 1;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_kit", idKit);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        KitUpsertModel model = new()
        {
            IdKit = reader.GetInt32("id_kit"),
            Codigo = reader.GetString("codigo"),
            Nombre = reader.GetString("nombre"),
            Descripcion = reader.GetString("descripcion"),
            PrecioVenta = reader.GetDecimal("precio_venta"),
            Activo = reader.GetBoolean("activo")
        };
        await reader.CloseAsync();

        const string detailSql = @"
SELECT kd.id_producto,
       p.codigo,
       p.nombre,
       COALESCE(p.unidad_medida, 'unidad') AS unidad_medida,
       p.precio_compra,
       p.precio_venta,
       COALESCE(i.cantidad, 0) AS stock_disponible,
       kd.cantidad
FROM kit_detalle kd
INNER JOIN productos p ON p.id_producto = kd.id_producto
LEFT JOIN inventario i ON i.id_producto = kd.id_producto AND (@warehouse_id IS NULL OR i.id_almacen = @warehouse_id)
WHERE kd.id_kit = @id_kit
ORDER BY p.nombre;";

        await using var detailCommand = new MySqlCommand(detailSql, connection);
        detailCommand.Parameters.AddWithValue("@id_kit", idKit);
        detailCommand.Parameters.AddWithValue("@warehouse_id", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        await using var detailReader = await detailCommand.ExecuteReaderAsync();
        while (await detailReader.ReadAsync())
        {
            model.Componentes.Add(new KitComponentDraft
            {
                IdProducto = detailReader.GetInt32("id_producto"),
                Codigo = detailReader.GetString("codigo"),
                Nombre = detailReader.GetString("nombre"),
                UnidadMedida = detailReader.GetString("unidad_medida"),
                PrecioCompra = detailReader.GetDecimal("precio_compra"),
                PrecioVenta = detailReader.GetDecimal("precio_venta"),
                StockDisponible = detailReader.GetInt32("stock_disponible"),
                Cantidad = detailReader.GetInt32("cantidad")
            });
        }

        return model;
    }

    public async Task SaveAsync(KitUpsertModel model)
    {
        await EnsureSchemaAsync();

        if (model.Componentes.Count == 0)
        {
            throw new InvalidOperationException("El kit debe tener al menos un componente.");
        }

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        string sql = model.IdKit == 0
            ? @"INSERT INTO kits (codigo, nombre, descripcion, precio_venta, activo)
                VALUES (@codigo, @nombre, @descripcion, @precio_venta, @activo);"
            : @"UPDATE kits
                SET codigo = @codigo,
                    nombre = @nombre,
                    descripcion = @descripcion,
                    precio_venta = @precio_venta,
                    activo = @activo
                WHERE id_kit = @id_kit;";

        await using var command = new MySqlCommand(sql, connection, transaction);
        if (model.IdKit != 0)
        {
            command.Parameters.AddWithValue("@id_kit", model.IdKit);
        }

        command.Parameters.AddWithValue("@codigo", model.Codigo.Trim());
        command.Parameters.AddWithValue("@nombre", model.Nombre.Trim());
        command.Parameters.AddWithValue("@descripcion", string.IsNullOrWhiteSpace(model.Descripcion) ? DBNull.Value : model.Descripcion.Trim());
        command.Parameters.AddWithValue("@precio_venta", model.PrecioVenta);
        command.Parameters.AddWithValue("@activo", model.Activo);
        await command.ExecuteNonQueryAsync();

        int idKit = model.IdKit == 0 ? (int)command.LastInsertedId : model.IdKit;

        const string deleteDetailSql = "DELETE FROM kit_detalle WHERE id_kit = @id_kit;";
        await using (var deleteCommand = new MySqlCommand(deleteDetailSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("@id_kit", idKit);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        if (model.Componentes.Count > 0)
        {
            var values = new System.Text.StringBuilder();
            for (int i = 0; i < model.Componentes.Count; i++)
            {
                var component = model.Componentes[i];
                if (i > 0) values.Append(",");
                values.Append($"(@id_kit, @id_producto_{i}, @cantidad_{i})");
            }

            string detailSql = $"INSERT INTO kit_detalle (id_kit, id_producto, cantidad) VALUES {values};";
            await using var detailCommand = new MySqlCommand(detailSql, connection, transaction);
            detailCommand.Parameters.AddWithValue("@id_kit", idKit);
            for (int i = 0; i < model.Componentes.Count; i++)
            {
                var component = model.Componentes[i];
                detailCommand.Parameters.AddWithValue($"@id_producto_{i}", component.IdProducto);
                detailCommand.Parameters.AddWithValue($"@cantidad_{i}", component.Cantidad);
            }
            await detailCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(int idKit)
    {
        await EnsureSchemaAsync();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string usageSql = @"
SELECT
    (SELECT COUNT(*) FROM detalle_nota_venta WHERE id_kit = @id_kit) +
    (SELECT COUNT(*) FROM detalle_cotizacion WHERE id_kit = @id_kit);";
        await using var usageCommand = new MySqlCommand(usageSql, connection);
        usageCommand.Parameters.AddWithValue("@id_kit", idKit);
        int usageCount = Convert.ToInt32(await usageCommand.ExecuteScalarAsync() ?? 0);
        if (usageCount > 0)
        {
            throw new InvalidOperationException("No se puede eliminar el kit porque ya fue usado en cotizaciones o ventas.");
        }

        await using var transaction = await connection.BeginTransactionAsync();
        await using (var deleteDetailCommand = new MySqlCommand("DELETE FROM kit_detalle WHERE id_kit = @id_kit;", connection, transaction))
        {
            deleteDetailCommand.Parameters.AddWithValue("@id_kit", idKit);
            await deleteDetailCommand.ExecuteNonQueryAsync();
        }

        await using (var deleteKitCommand = new MySqlCommand("DELETE FROM kits WHERE id_kit = @id_kit;", connection, transaction))
        {
            deleteKitCommand.Parameters.AddWithValue("@id_kit", idKit);
            await deleteKitCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<List<KitComponentDraft>> GetKitComponentsAsync(int idKit, int? warehouseId = null)
    {
        await EnsureSchemaAsync();

        const string sql = @"
SELECT kd.id_producto,
       p.codigo,
       p.nombre,
       COALESCE(p.unidad_medida, 'unidad') AS unidad_medida,
       p.precio_compra,
       p.precio_venta,
       COALESCE(i.cantidad, 0) AS stock_disponible,
       kd.cantidad
FROM kit_detalle kd
INNER JOIN productos p ON p.id_producto = kd.id_producto
LEFT JOIN inventario i ON i.id_producto = kd.id_producto AND (@warehouse_id IS NULL OR i.id_almacen = @warehouse_id)
WHERE kd.id_kit = @id_kit
ORDER BY p.nombre;";

        List<KitComponentDraft> items = new();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_kit", idKit);
        command.Parameters.AddWithValue("@warehouse_id", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new KitComponentDraft
            {
                IdProducto = reader.GetInt32("id_producto"),
                Codigo = reader.GetString("codigo"),
                Nombre = reader.GetString("nombre"),
                UnidadMedida = reader.GetString("unidad_medida"),
                PrecioCompra = reader.GetDecimal("precio_compra"),
                PrecioVenta = reader.GetDecimal("precio_venta"),
                StockDisponible = reader.GetInt32("stock_disponible"),
                Cantidad = reader.GetInt32("cantidad")
            });
        }

        return items;
    }

    private async Task EnsureSchemaAsync()
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaLock.WaitAsync();
        try
        {
            if (_schemaReady)
            {
                return;
            }

            const string kitTableSql = @"
CREATE TABLE IF NOT EXISTS kits (
  id_kit INT NOT NULL AUTO_INCREMENT,
  codigo VARCHAR(40) NOT NULL UNIQUE,
  nombre VARCHAR(150) NOT NULL,
  descripcion TEXT NULL,
  precio_venta DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  activo TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (id_kit)
) ENGINE=InnoDB;";

            const string kitDetailSql = @"
CREATE TABLE IF NOT EXISTS kit_detalle (
  id_detalle_kit INT NOT NULL AUTO_INCREMENT,
  id_kit INT NOT NULL,
  id_producto INT NOT NULL,
  cantidad INT NOT NULL,
  PRIMARY KEY (id_detalle_kit),
  CONSTRAINT fk_kit_det_kit FOREIGN KEY (id_kit) REFERENCES kits (id_kit),
  CONSTRAINT fk_kit_det_prod FOREIGN KEY (id_producto) REFERENCES productos (id_producto)
) ENGINE=InnoDB;";

            await using var connection = DbConnectionFactory.CreateConnection();
            await connection.OpenAsync();
            await using var kitCommand = new MySqlCommand(kitTableSql, connection);
            await kitCommand.ExecuteNonQueryAsync();
            await using var detailCommand = new MySqlCommand(kitDetailSql, connection);
            await detailCommand.ExecuteNonQueryAsync();

            await EnsureColumnAsync(connection, "detalle_nota_venta", "id_kit", "ALTER TABLE detalle_nota_venta ADD COLUMN id_kit INT NULL AFTER id_producto;");
            await EnsureColumnAsync(connection, "detalle_nota_venta", "es_kit", "ALTER TABLE detalle_nota_venta ADD COLUMN es_kit TINYINT(1) NOT NULL DEFAULT 0 AFTER id_kit;");
            await EnsureColumnAsync(connection, "detalle_nota_venta", "codigo_item", "ALTER TABLE detalle_nota_venta ADD COLUMN codigo_item VARCHAR(60) NULL AFTER subtotal;");
            await EnsureColumnAsync(connection, "detalle_nota_venta", "nombre_item", "ALTER TABLE detalle_nota_venta ADD COLUMN nombre_item VARCHAR(180) NULL AFTER codigo_item;");
            await EnsureColumnAsync(connection, "detalle_nota_venta", "descripcion_item", "ALTER TABLE detalle_nota_venta ADD COLUMN descripcion_item TEXT NULL AFTER nombre_item;");
            await EnsureColumnAsync(connection, "detalle_nota_venta", "unidad_medida_item", "ALTER TABLE detalle_nota_venta ADD COLUMN unidad_medida_item VARCHAR(30) NULL AFTER descripcion_item;");

            await EnsureColumnAsync(connection, "detalle_cotizacion", "id_kit", "ALTER TABLE detalle_cotizacion ADD COLUMN id_kit INT NULL AFTER id_producto;");
            await EnsureColumnAsync(connection, "detalle_cotizacion", "es_kit", "ALTER TABLE detalle_cotizacion ADD COLUMN es_kit TINYINT(1) NOT NULL DEFAULT 0 AFTER id_kit;");
            await EnsureColumnAsync(connection, "detalle_cotizacion", "codigo_item", "ALTER TABLE detalle_cotizacion ADD COLUMN codigo_item VARCHAR(60) NULL AFTER subtotal;");
            await EnsureColumnAsync(connection, "detalle_cotizacion", "nombre_item", "ALTER TABLE detalle_cotizacion ADD COLUMN nombre_item VARCHAR(180) NULL AFTER codigo_item;");
            await EnsureColumnAsync(connection, "detalle_cotizacion", "descripcion_item", "ALTER TABLE detalle_cotizacion ADD COLUMN descripcion_item TEXT NULL AFTER nombre_item;");
            await EnsureColumnAsync(connection, "detalle_cotizacion", "unidad_medida_item", "ALTER TABLE detalle_cotizacion ADD COLUMN unidad_medida_item VARCHAR(30) NULL AFTER descripcion_item;");

            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private static async Task EnsureColumnAsync(MySqlConnection connection, string tableName, string columnName, string alterSql)
    {
        const string existsSql = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @table_name
  AND COLUMN_NAME = @column_name;";

        await using var existsCommand = new MySqlCommand(existsSql, connection);
        existsCommand.Parameters.AddWithValue("@table_name", tableName);
        existsCommand.Parameters.AddWithValue("@column_name", columnName);
        int exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync() ?? 0);
        if (exists > 0)
        {
            return;
        }

        await using var alterCommand = new MySqlCommand(alterSql, connection);
        await alterCommand.ExecuteNonQueryAsync();
    }
}
