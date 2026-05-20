using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class QuotationRepository
{
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaReady;
    private readonly KitRepository _kitRepository;

    public QuotationRepository(KitRepository kitRepository)
    {
        _kitRepository = kitRepository;
    }

    public async Task<string> CreateAsync(QuotationSaveRequest request)
    {
        await EnsureSchemaAsync();
        await _kitRepository.EnsureSchemaReadyAsync();

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string quotationSql = @"
INSERT INTO cotizaciones
    (codigo, id_cliente, nombre_cliente, ci_nit_cliente, id_usuario, id_almacen, fecha, estado, subtotal, descuento, total, observaciones)
VALUES
    (@codigo, @id_cliente, @nombre_cliente, @ci_nit_cliente, @id_usuario, @id_almacen, @fecha, 'generada', @subtotal, @descuento, @total, @observaciones);";

        await using var quotationCommand = new MySqlCommand(quotationSql, connection, transaction);
        quotationCommand.Parameters.AddWithValue("@codigo", $"TMP-{Guid.NewGuid():N}");
        quotationCommand.Parameters.AddWithValue("@id_cliente", request.IdCliente.HasValue ? request.IdCliente.Value : DBNull.Value);
        quotationCommand.Parameters.AddWithValue("@nombre_cliente", string.IsNullOrWhiteSpace(request.NombreCliente) ? DBNull.Value : request.NombreCliente);
        quotationCommand.Parameters.AddWithValue("@ci_nit_cliente", string.IsNullOrWhiteSpace(request.CiNitCliente) ? DBNull.Value : request.CiNitCliente);
        quotationCommand.Parameters.AddWithValue("@id_usuario", request.IdUsuario);
        quotationCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
        quotationCommand.Parameters.AddWithValue("@fecha", request.Fecha);
        quotationCommand.Parameters.AddWithValue("@subtotal", request.Subtotal);
        quotationCommand.Parameters.AddWithValue("@descuento", request.Descuento);
        quotationCommand.Parameters.AddWithValue("@total", request.Total);
        quotationCommand.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones);
        await quotationCommand.ExecuteNonQueryAsync();
        int quotationId = (int)quotationCommand.LastInsertedId;
        string quotationCode = BuildQuotationCode(quotationId);

        const string updateCodeSql = "UPDATE cotizaciones SET codigo = @codigo WHERE id_cotizacion = @id_cotizacion;";
        await using var updateCodeCommand = new MySqlCommand(updateCodeSql, connection, transaction);
        updateCodeCommand.Parameters.AddWithValue("@codigo", quotationCode);
        updateCodeCommand.Parameters.AddWithValue("@id_cotizacion", quotationId);
        await updateCodeCommand.ExecuteNonQueryAsync();

        const string detailSql = @"
INSERT INTO detalle_cotizacion
    (id_cotizacion, id_producto, id_kit, es_kit, cantidad, precio_unitario, tipo_descuento, descuento_valor, descuento, subtotal, codigo_item, nombre_item, descripcion_item, unidad_medida_item)
VALUES
    (@id_cotizacion, @id_producto, @id_kit, @es_kit, @cantidad, @precio_unitario, @tipo_descuento, @descuento_valor, @descuento, @subtotal, @codigo_item, @nombre_item, @descripcion_item, @unidad_medida_item);";

        foreach (SaleDetailDraft item in request.Items)
        {
            await using var detailCommand = new MySqlCommand(detailSql, connection, transaction);
            detailCommand.Parameters.AddWithValue("@id_cotizacion", quotationId);
            detailCommand.Parameters.AddWithValue("@id_producto", item.IsKit ? DBNull.Value : item.IdProducto);
            detailCommand.Parameters.AddWithValue("@id_kit", item.IsKit && item.IdKit.HasValue ? item.IdKit.Value : DBNull.Value);
            detailCommand.Parameters.AddWithValue("@es_kit", item.IsKit);
            detailCommand.Parameters.AddWithValue("@cantidad", item.Cantidad);
            detailCommand.Parameters.AddWithValue("@precio_unitario", item.PrecioUnitario);
            detailCommand.Parameters.AddWithValue("@tipo_descuento", item.TipoDescuento);
            detailCommand.Parameters.AddWithValue("@descuento_valor", item.DescuentoValor);
            detailCommand.Parameters.AddWithValue("@descuento", item.Descuento);
            detailCommand.Parameters.AddWithValue("@subtotal", item.Subtotal);
            detailCommand.Parameters.AddWithValue("@codigo_item", item.Codigo);
            detailCommand.Parameters.AddWithValue("@nombre_item", item.Producto);
            detailCommand.Parameters.AddWithValue("@descripcion_item", string.IsNullOrWhiteSpace(item.Descripcion) ? item.Producto : item.Descripcion);
            detailCommand.Parameters.AddWithValue("@unidad_medida_item", item.UnidadMedida);
            await detailCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return quotationCode;
    }

    public async Task<QuotationLoadResult?> GetByCodeAsync(string code)
    {
        await EnsureSchemaAsync();
        await _kitRepository.EnsureSchemaReadyAsync();

        const string quotationSql = @"
SELECT id_cotizacion, codigo, id_cliente, nombre_cliente, ci_nit_cliente, id_almacen, estado, descuento, observaciones
FROM cotizaciones
WHERE codigo = @codigo
LIMIT 1;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var quotationCommand = new MySqlCommand(quotationSql, connection);
        quotationCommand.Parameters.AddWithValue("@codigo", code);
        await using var quotationReader = await quotationCommand.ExecuteReaderAsync();

        if (!await quotationReader.ReadAsync())
        {
            return null;
        }

        QuotationLoadResult result = new()
        {
            IdCotizacion = quotationReader.GetInt32("id_cotizacion"),
            Codigo = quotationReader.GetString("codigo"),
            IdCliente = IsDBNull(quotationReader, "id_cliente") ? null : quotationReader.GetInt32("id_cliente"),
            NombreCliente = GetStringSafe(quotationReader, "nombre_cliente"),
            CiNitCliente = GetStringSafe(quotationReader, "ci_nit_cliente"),
            IdAlmacen = quotationReader.GetInt32("id_almacen"),
            Estado = GetStringSafe(quotationReader, "estado", "generada"),
            Descuento = quotationReader.GetDecimal("descuento"),
            Observaciones = GetStringSafe(quotationReader, "observaciones")
        };
        await quotationReader.CloseAsync();

        const string detailSql = @"
SELECT dc.id_producto,
       dc.id_kit,
       dc.es_kit,
       COALESCE(dc.codigo_item, k.codigo, p.codigo, '') AS codigo,
       CASE WHEN dc.es_kit = 1 THEN '' ELSE COALESCE(p.codigo_barras, '') END AS codigo_barras,
       COALESCE(dc.nombre_item, k.nombre, p.nombre, '') AS producto,
       COALESCE(dc.descripcion_item, k.descripcion, p.descripcion, p.nombre, '') AS descripcion,
       COALESCE(dc.unidad_medida_item, CASE WHEN dc.es_kit = 1 THEN 'kit' ELSE p.unidad_medida END, 'unidad') AS unidad_medida,
       dc.cantidad,
       dc.precio_unitario,
       dc.tipo_descuento,
       dc.descuento_valor
FROM detalle_cotizacion dc
LEFT JOIN productos p ON p.id_producto = dc.id_producto
LEFT JOIN kits k ON k.id_kit = dc.id_kit
WHERE dc.id_cotizacion = @id_cotizacion
ORDER BY dc.id_detalle;";

        await using var detailCommand = new MySqlCommand(detailSql, connection);
        detailCommand.Parameters.AddWithValue("@id_cotizacion", result.IdCotizacion);
        await using var detailReader = await detailCommand.ExecuteReaderAsync();

        while (await detailReader.ReadAsync())
        {
            result.Items.Add(new SaleDetailDraft
            {
                IdProducto = detailReader.IsDBNull(detailReader.GetOrdinal("id_producto")) ? 0 : detailReader.GetInt32("id_producto"),
                IdKit = detailReader.IsDBNull(detailReader.GetOrdinal("id_kit")) ? null : detailReader.GetInt32("id_kit"),
                IsKit = detailReader.GetBoolean("es_kit"),
                Codigo = detailReader.GetString("codigo"),
                CodigoBarras = GetStringSafe(detailReader, "codigo_barras"),
                Producto = detailReader.GetString("producto"),
                Descripcion = detailReader.GetString("descripcion"),
                UnidadMedida = detailReader.GetString("unidad_medida"),
                Cantidad = detailReader.GetInt32("cantidad"),
                PrecioUnitario = detailReader.GetDecimal("precio_unitario"),
                TipoDescuento = detailReader.GetString("tipo_descuento"),
                DescuentoValor = detailReader.GetDecimal("descuento_valor")
            });
        }

        return result;
    }

    public async Task MarkAsConvertedAsync(int quotationId, int saleId)
    {
        await EnsureSchemaAsync();
        await _kitRepository.EnsureSchemaReadyAsync();

        const string sql = @"
UPDATE cotizaciones
SET estado = 'convertida',
    id_nota_convertida = @id_nota_convertida
WHERE id_cotizacion = @id_cotizacion;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id_cotizacion", quotationId);
        command.Parameters.AddWithValue("@id_nota_convertida", saleId);
        await command.ExecuteNonQueryAsync();
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

            const string quotationTableSql = @"
CREATE TABLE IF NOT EXISTS cotizaciones (
  id_cotizacion INT NOT NULL AUTO_INCREMENT,
  codigo VARCHAR(30) NOT NULL UNIQUE,
  id_cliente INT NULL,
  nombre_cliente VARCHAR(150) NULL,
  ci_nit_cliente VARCHAR(20) NULL,
  id_usuario INT NOT NULL,
  id_almacen INT NOT NULL,
  fecha DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  estado VARCHAR(20) NOT NULL DEFAULT 'generada',
  subtotal DECIMAL(14,2) NOT NULL DEFAULT 0.00,
  descuento DECIMAL(14,2) NOT NULL DEFAULT 0.00,
  total DECIMAL(14,2) NOT NULL DEFAULT 0.00,
  observaciones TEXT NULL,
  id_nota_convertida INT NULL,
  PRIMARY KEY (id_cotizacion),
  CONSTRAINT fk_cot_cli FOREIGN KEY (id_cliente) REFERENCES clientes (id_cliente),
  CONSTRAINT fk_cot_usr FOREIGN KEY (id_usuario) REFERENCES usuarios (id_usuario),
  CONSTRAINT fk_cot_alm FOREIGN KEY (id_almacen) REFERENCES almacenes (id_almacen)
) ENGINE=InnoDB;";

            const string detailTableSql = @"
CREATE TABLE IF NOT EXISTS detalle_cotizacion (
  id_detalle INT NOT NULL AUTO_INCREMENT,
  id_cotizacion INT NOT NULL,
  id_producto INT NOT NULL,
  cantidad INT NOT NULL,
  precio_unitario DECIMAL(12,2) NOT NULL,
  tipo_descuento VARCHAR(20) NOT NULL DEFAULT 'Monto',
  descuento_valor DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  descuento DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  subtotal DECIMAL(14,2) NOT NULL DEFAULT 0.00,
  PRIMARY KEY (id_detalle),
  CONSTRAINT fk_dc_cot FOREIGN KEY (id_cotizacion) REFERENCES cotizaciones (id_cotizacion),
  CONSTRAINT fk_dc_prod FOREIGN KEY (id_producto) REFERENCES productos (id_producto)
) ENGINE=InnoDB;";

            await using var connection = DbConnectionFactory.CreateConnection();
            await connection.OpenAsync();
            await using var quotationTableCommand = new MySqlCommand(quotationTableSql, connection);
            await quotationTableCommand.ExecuteNonQueryAsync();
            await using var detailTableCommand = new MySqlCommand(detailTableSql, connection);
            await detailTableCommand.ExecuteNonQueryAsync();
            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private static string GetStringSafe(MySqlDataReader reader, string columnName, string defaultValue = "")
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
    }

    private static bool IsDBNull(MySqlDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal);
    }

    private static string BuildQuotationCode(int quotationId)
    {
        return $"COT-{quotationId:D5}";
    }
}
