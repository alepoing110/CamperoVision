using System.Text;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class SalesNoteRepository : ISalesNoteRepository
{
    private readonly KitRepository _kitRepository;
    private readonly ILogger<SalesNoteRepository> _logger;
    private readonly IAuditService _auditService;

    public SalesNoteRepository(KitRepository kitRepository, ILogger<SalesNoteRepository> logger, IAuditService auditService)
    {
        _kitRepository = kitRepository;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<SaleNoteEditModel> GetEditModelAsync(int idNota)
    {
        await _kitRepository.EnsureSchemaReadyAsync();

        const string noteSql = @"
SELECT id_nota, nro_nota, id_cliente, COALESCE(nombre_comprador, '') AS nombre_comprador,
       COALESCE(ci_nit_comprador, '') AS ci_nit_comprador, id_almacen, descuento, estado
FROM notas_venta
WHERE id_nota = @id_nota
LIMIT 1;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var noteCommand = new MySqlCommand(noteSql, connection);
        noteCommand.Parameters.AddWithValue("@id_nota", idNota);
        await using var noteReader = await noteCommand.ExecuteReaderAsync();
        if (!await noteReader.ReadAsync())
        {
            throw new InvalidOperationException("No se encontro la nota de venta.");
        }

        SaleNoteEditModel model = new()
        {
            IdNota = noteReader.GetInt32("id_nota"),
            NroNota = noteReader.GetString("nro_nota"),
            IdCliente = noteReader.GetNullableInt32("id_cliente"),
            BuyerName = noteReader.GetStringSafe("nombre_comprador"),
            BuyerCiNit = noteReader.GetStringSafe("ci_nit_comprador"),
            IdAlmacen = noteReader.GetInt32("id_almacen"),
            Estado = noteReader.GetString("estado")
        };
        decimal totalDiscount = noteReader.GetDecimal("descuento");
        await noteReader.CloseAsync();

        const string detailSql = @"
SELECT d.id_producto,
       d.id_kit,
       d.es_kit,
       COALESCE(d.codigo_item, k.codigo, p.codigo, '') AS codigo,
       COALESCE(d.nombre_item, k.nombre, p.nombre, '') AS producto,
       COALESCE(d.descripcion_item, k.descripcion, p.descripcion, p.nombre, '') AS descripcion,
       COALESCE(d.unidad_medida_item, CASE WHEN d.es_kit = 1 THEN 'kit' ELSE p.unidad_medida END, 'unidad') AS unidad_medida,
       d.cantidad,
       d.precio_unitario,
       d.descuento
FROM detalle_nota_venta d
LEFT JOIN productos p ON p.id_producto = d.id_producto
LEFT JOIN kits k ON k.id_kit = d.id_kit
WHERE d.id_nota = @id_nota
ORDER BY d.id_detalle;";

        await using var detailCommand = new MySqlCommand(detailSql, connection);
        detailCommand.Parameters.AddWithValue("@id_nota", idNota);
        await using var detailReader = await detailCommand.ExecuteReaderAsync();

        while (await detailReader.ReadAsync())
        {
            model.Items.Add(new SaleNoteEditDetailModel
            {
                IdProducto = detailReader.IsDBNull(detailReader.GetOrdinal("id_producto")) ? 0 : detailReader.GetInt32("id_producto"),
                IdKit = detailReader.IsDBNull(detailReader.GetOrdinal("id_kit")) ? null : detailReader.GetInt32("id_kit"),
                IsKit = detailReader.GetBoolean("es_kit"),
                Codigo = detailReader.GetString("codigo"),
                Producto = detailReader.GetString("producto"),
                Descripcion = detailReader.GetString("descripcion"),
                UnidadMedida = detailReader.GetString("unidad_medida"),
                Cantidad = detailReader.GetInt32("cantidad"),
                PrecioUnitario = detailReader.GetDecimal("precio_unitario"),
                DescuentoMonto = detailReader.GetDecimal("descuento")
            });
        }

        decimal itemDiscounts = model.Items.Sum(i => i.DescuentoAplicado);
        model.GeneralDiscount = Math.Max(totalDiscount - itemDiscounts, 0);
        return model;
    }

    public async Task UpdateAsync(SaleNoteEditModel model)
    {
        await _kitRepository.EnsureSchemaReadyAsync();

        if (model.Items.Count == 0)
        {
            throw new InvalidOperationException("La nota debe tener al menos un producto.");
        }

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string paymentsSql = "SELECT COUNT(*) FROM pagos WHERE id_nota = @id_nota;";
        await using var paymentsCommand = new MySqlCommand(paymentsSql, connection, transaction);
        paymentsCommand.Parameters.AddWithValue("@id_nota", model.IdNota);
        int paymentCount = Convert.ToInt32(await paymentsCommand.ExecuteScalarAsync() ?? 0);
        if (paymentCount > 0)
        {
            throw new InvalidOperationException("No se puede editar una nota que ya tiene pagos registrados.");
        }

        const string warehouseSql = "SELECT id_almacen FROM notas_venta WHERE id_nota = @id_nota AND estado = 'completada' LIMIT 1;";
        await using var warehouseCommand = new MySqlCommand(warehouseSql, connection, transaction);
        warehouseCommand.Parameters.AddWithValue("@id_nota", model.IdNota);
        object? warehouseObj = await warehouseCommand.ExecuteScalarAsync();
        if (warehouseObj is null)
        {
            throw new InvalidOperationException("La nota no existe o no esta en estado completada.");
        }

        int warehouseId = Convert.ToInt32(warehouseObj);
        List<SaleNoteEditDetailModel> oldItems = await GetStoredSaleItemsAsync(model.IdNota, connection, transaction);

        await ApplyInventoryDeltaAsync(oldItems, warehouseId, +1, connection, transaction);
        await ValidateStockAsync(model.Items, warehouseId, connection, transaction);

        const string deleteDetailsSql = "DELETE FROM detalle_nota_venta WHERE id_nota = @id_nota;";
        await using var deleteDetailsCommand = new MySqlCommand(deleteDetailsSql, connection, transaction);
        deleteDetailsCommand.Parameters.AddWithValue("@id_nota", model.IdNota);
        await deleteDetailsCommand.ExecuteNonQueryAsync();

        const string deleteMovementsSql = "DELETE FROM movimientos_stock WHERE referencia_id = @id_nota AND tipo = 'salida' AND motivo LIKE 'Venta registrada%';";
        await using var deleteMovementsCommand = new MySqlCommand(deleteMovementsSql, connection, transaction);
        deleteMovementsCommand.Parameters.AddWithValue("@id_nota", model.IdNota);
        await deleteMovementsCommand.ExecuteNonQueryAsync();

        decimal subtotal = model.Items.Sum(i => i.BaseSubtotal);
        decimal discountItems = model.Items.Sum(i => i.DescuentoAplicado);
        decimal totalDiscount = Math.Min(subtotal, discountItems + Math.Max(model.GeneralDiscount, 0));
        decimal total = subtotal - totalDiscount;

        const string updateSaleSql = @"
UPDATE notas_venta
SET nombre_comprador = @nombre_comprador,
    ci_nit_comprador = @ci_nit_comprador,
    subtotal = @subtotal,
    descuento = @descuento,
    total = @total
WHERE id_nota = @id_nota;";
        await using var updateSaleCommand = new MySqlCommand(updateSaleSql, connection, transaction);
        updateSaleCommand.Parameters.AddWithValue("@id_nota", model.IdNota);
        updateSaleCommand.Parameters.AddWithValue("@nombre_comprador", string.IsNullOrWhiteSpace(model.BuyerName) ? DBNull.Value : model.BuyerName);
        updateSaleCommand.Parameters.AddWithValue("@ci_nit_comprador", string.IsNullOrWhiteSpace(model.BuyerCiNit) ? DBNull.Value : model.BuyerCiNit);
        updateSaleCommand.Parameters.AddWithValue("@subtotal", subtotal);
        updateSaleCommand.Parameters.AddWithValue("@descuento", totalDiscount);
        updateSaleCommand.Parameters.AddWithValue("@total", total);
        await updateSaleCommand.ExecuteNonQueryAsync();

        foreach (SaleNoteEditDetailModel item in model.Items)
        {
            await InsertSaleDetailAsync(model.IdNota, item, connection, transaction);
            await ApplyInventoryDeltaAsync(new[] { item }, warehouseId, -1, connection, transaction);
            await InsertMovementsAsync(new[] { item }, warehouseId, model.EditorUserId, model.IdNota, connection, transaction);
        }

        await transaction.CommitAsync();
    }

    public async Task<List<SaleNoteListItem>> GetAllAsync(int page = 1, int pageSize = 100)
    {
        const string sql = @"
SELECT nv.id_nota, nv.nro_nota, COALESCE(c.nombre, nv.nombre_comprador, 'Sin cliente') AS cliente,
       u.nombre AS usuario, a.nombre AS almacen, nv.fecha, nv.estado, nv.total
FROM notas_venta nv
LEFT JOIN clientes c ON c.id_cliente = nv.id_cliente
INNER JOIN usuarios u ON u.id_usuario = nv.id_usuario
INNER JOIN almacenes a ON a.id_almacen = nv.id_almacen
ORDER BY nv.fecha DESC, nv.id_nota DESC
LIMIT @offset, @pageSize;";

        var items = new List<SaleNoteListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@offset", Math.Max(0, (page - 1) * pageSize));
        command.Parameters.AddWithValue("@pageSize", pageSize);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SaleNoteListItem
            {
                IdNota = reader.GetInt32("id_nota"),
                NroNota = reader.GetString("nro_nota"),
                Cliente = reader.GetString("cliente"),
                Usuario = reader.GetString("usuario"),
                Almacen = reader.GetString("almacen"),
                Fecha = reader.GetDateTime("fecha"),
                Estado = reader.GetString("estado"),
                Total = reader.GetDecimal("total")
            });
        }
        return items;
    }

    public async Task<SaleCreatedResult> CreateSaleAsync(SaleCreateRequest request)
    {
        _logger.LogInformation("Creando venta: {ItemCount} items, almacen {WarehouseId}, usuario {UserId}", request.Items.Count, request.IdAlmacen, request.IdUsuario);
        await _kitRepository.EnsureSchemaReadyAsync();

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Agrega al menos un producto a la venta.");
        }

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string correlativoSql = "SELECT id_correlativo, prefijo, anio, ultimo_nro FROM correlativo_nota WHERE id_almacen=@id_almacen LIMIT 1 FOR UPDATE;";
        await using var correlativoCommand = new MySqlCommand(correlativoSql, connection, transaction);
        correlativoCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
        await using var correlativoReader = await correlativoCommand.ExecuteReaderAsync();

        int idCorrelativo = 0;
        string prefijo = "NV";
        int anio = DateTime.Today.Year;
        int ultimoNro = 0;

        if (await correlativoReader.ReadAsync())
        {
            idCorrelativo = correlativoReader.GetInt32("id_correlativo");
            prefijo = correlativoReader.GetString("prefijo");
            anio = Convert.ToInt32(correlativoReader["anio"]);
            ultimoNro = correlativoReader.GetInt32("ultimo_nro");
        }
        await correlativoReader.CloseAsync();

        if (idCorrelativo == 0)
        {
            const string insertCorrelativo = "INSERT INTO correlativo_nota (id_almacen, prefijo, anio, ultimo_nro) VALUES (@id_almacen, 'NV', @anio, 0);";
            await using var insertCorrelativoCommand = new MySqlCommand(insertCorrelativo, connection, transaction);
            insertCorrelativoCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
            insertCorrelativoCommand.Parameters.AddWithValue("@anio", DateTime.Today.Year);
            await insertCorrelativoCommand.ExecuteNonQueryAsync();
            idCorrelativo = (int)insertCorrelativoCommand.LastInsertedId;
            anio = DateTime.Today.Year;
            ultimoNro = 0;
        }

        if (anio != DateTime.Today.Year)
        {
            anio = DateTime.Today.Year;
            ultimoNro = 0;
            const string resetCorrelativo = "UPDATE correlativo_nota SET anio=@anio, ultimo_nro=@ultimo_nro WHERE id_correlativo=@id_correlativo;";
            await using var resetCommand = new MySqlCommand(resetCorrelativo, connection, transaction);
            resetCommand.Parameters.AddWithValue("@anio", anio);
            resetCommand.Parameters.AddWithValue("@ultimo_nro", 0);
            resetCommand.Parameters.AddWithValue("@id_correlativo", idCorrelativo);
            await resetCommand.ExecuteNonQueryAsync();
        }

        await ValidateStockAsync(request.Items.Select(ToEditModel).ToList(), request.IdAlmacen, connection, transaction);

        ultimoNro++;
        string nroNota = BuildWarehouseNoteNumber(prefijo, request.IdAlmacen, anio, ultimoNro);
        DateTime fechaVenta = DateTime.Now;
        decimal subtotal = request.Items.Sum(i => i.BaseSubtotal);
        decimal descuentoItems = request.Items.Sum(i => i.Descuento);
        decimal descuentoTotal = descuentoItems + request.DescuentoAdicional;
        if (descuentoTotal > subtotal)
        {
            descuentoTotal = subtotal;
        }
        decimal total = subtotal - descuentoTotal;

        const string saleSql = @"INSERT INTO notas_venta (nro_nota, id_cliente, nombre_comprador, ci_nit_comprador, id_usuario, id_almacen, fecha, estado, subtotal, descuento, total, observaciones)
                                 VALUES (@nro_nota, @id_cliente, @nombre_comprador, @ci_nit_comprador, @id_usuario, @id_almacen, @fecha, 'completada', @subtotal, @descuento, @total, @observaciones);";
        await using var saleCommand = new MySqlCommand(saleSql, connection, transaction);
        saleCommand.Parameters.AddWithValue("@nro_nota", nroNota);
        saleCommand.Parameters.AddWithValue("@id_cliente", request.IdCliente.HasValue ? request.IdCliente.Value : DBNull.Value);
        saleCommand.Parameters.AddWithValue("@nombre_comprador", string.IsNullOrWhiteSpace(request.NombreComprador) ? DBNull.Value : request.NombreComprador);
        saleCommand.Parameters.AddWithValue("@ci_nit_comprador", string.IsNullOrWhiteSpace(request.CiNitComprador) ? DBNull.Value : request.CiNitComprador);
        saleCommand.Parameters.AddWithValue("@id_usuario", request.IdUsuario);
        saleCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
        saleCommand.Parameters.AddWithValue("@fecha", fechaVenta);
        saleCommand.Parameters.AddWithValue("@subtotal", subtotal);
        saleCommand.Parameters.AddWithValue("@descuento", descuentoTotal);
        saleCommand.Parameters.AddWithValue("@total", total);
        saleCommand.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones);
        await saleCommand.ExecuteNonQueryAsync();
        int idNota = (int)saleCommand.LastInsertedId;

        foreach (SaleDetailDraft item in request.Items)
        {
            SaleNoteEditDetailModel editableItem = ToEditModel(item);
            await InsertSaleDetailAsync(idNota, editableItem, connection, transaction);
            await ApplyInventoryDeltaAsync(new[] { editableItem }, request.IdAlmacen, -1, connection, transaction);
            await InsertMovementsAsync(new[] { editableItem }, request.IdAlmacen, request.IdUsuario, idNota, connection, transaction);
        }

        const string updateCorrelativo = "UPDATE correlativo_nota SET anio=@anio, ultimo_nro=@ultimo_nro WHERE id_correlativo=@id_correlativo;";
        await using var updateCorrelativoCommand = new MySqlCommand(updateCorrelativo, connection, transaction);
        updateCorrelativoCommand.Parameters.AddWithValue("@anio", anio);
        updateCorrelativoCommand.Parameters.AddWithValue("@ultimo_nro", ultimoNro);
        updateCorrelativoCommand.Parameters.AddWithValue("@id_correlativo", idCorrelativo);
        await updateCorrelativoCommand.ExecuteNonQueryAsync();

        await transaction.CommitAsync();

        _logger.LogInformation("Venta creada exitosamente: Nota {NroNota}, ID {IdNota}, Total {Total}", nroNota, idNota, total);

        await _auditService.LogAsync(
            "notas_venta",
            "CREATE",
            idNota,
            $"Nota de venta creada: {nroNota} - Total: {total:C}",
            null,
            new { nro_nota = nroNota, id_cliente = request.IdCliente, total, descuento = descuentoTotal, items = request.Items.Count });

        return new SaleCreatedResult
        {
            IdNota = idNota,
            NroNota = nroNota,
            Fecha = fechaVenta,
            Subtotal = subtotal,
            Descuento = descuentoTotal,
            Total = total
        };
    }

    private static string BuildWarehouseNoteNumber(string prefijo, int warehouseId, int year, int sequence)
    {
        string normalizedPrefix = string.IsNullOrWhiteSpace(prefijo) ? "NV" : prefijo.Trim().ToUpperInvariant();
        return $"{normalizedPrefix}-ALM{warehouseId:D2}-{year}-{sequence:D5}";
    }

    public async Task AnularNotaAsync(int idNota, int idUsuario, string motivo)
    {
        _logger.LogWarning("Anulando nota {IdNota} por usuario {UserId}. Motivo: {Motivo}", idNota, idUsuario, motivo);
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string checkPago = "SELECT COUNT(*) FROM pagos WHERE id_nota = @idNota;";
        await using var pagoCmd = new MySqlCommand(checkPago, connection, transaction);
        pagoCmd.Parameters.AddWithValue("@idNota", idNota);
        object? pagoCountObj = await pagoCmd.ExecuteScalarAsync();
        int pagos = Convert.ToInt32(pagoCountObj ?? 0);
        if (pagos > 0) throw new InvalidOperationException("Nota con pagos no puede ser anulada.");

        const string notaSql = "SELECT id_almacen FROM notas_venta WHERE id_nota = @idNota AND estado = 'completada';";
        await using var notaCmd = new MySqlCommand(notaSql, connection, transaction);
        notaCmd.Parameters.AddWithValue("@idNota", idNota);
        object? almObj = await notaCmd.ExecuteScalarAsync();
        if (almObj == null) throw new InvalidOperationException("Nota no encontrada o ya anulada.");

        int idAlmacen = Convert.ToInt32(almObj);

        // Obtener los items originales de la nota
        List<SaleNoteEditDetailModel> items = await GetStoredSaleItemsAsync(idNota, connection, transaction);

        // Restaurar inventario (sumar cantidades de vuelta)
        await ApplyInventoryDeltaAsync(items, idAlmacen, +1, connection, transaction);

        // Registrar movimiento de stock de anulacion
        foreach (SaleNoteEditDetailModel item in items)
        {
            foreach ((int productId, int quantity, string componentName) in await ExpandItemComponentsAsync(item, connection, transaction))
            {
                const string movementSql = @"
INSERT INTO movimientos_stock (id_producto, id_almacen, id_usuario, tipo, cantidad, motivo, referencia_id)
VALUES (@id_producto, @id_almacen, @id_usuario, 'entrada', @cantidad, @motivo, @id_nota);";
                await using var movementCommand = new MySqlCommand(movementSql, connection, transaction);
                movementCommand.Parameters.AddWithValue("@id_producto", productId);
                movementCommand.Parameters.AddWithValue("@id_almacen", idAlmacen);
                movementCommand.Parameters.AddWithValue("@id_usuario", idUsuario);
                movementCommand.Parameters.AddWithValue("@cantidad", quantity);
                movementCommand.Parameters.AddWithValue("@motivo", item.IsKit
                    ? $"Nota anulada | Kit: {item.Producto} | Componente: {componentName}"
                    : "Nota anulada");
                movementCommand.Parameters.AddWithValue("@id_nota", idNota);
                await movementCommand.ExecuteNonQueryAsync();
            }
        }

        const string updateEstado = "UPDATE notas_venta SET estado = 'anulada', motivo_anulacion = @motivo WHERE id_nota = @idNota;";
        await using var stateCmd = new MySqlCommand(updateEstado, connection, transaction);
        stateCmd.Parameters.AddWithValue("@idNota", idNota);
        stateCmd.Parameters.AddWithValue("@motivo", motivo);
        await stateCmd.ExecuteNonQueryAsync();

        await transaction.CommitAsync();

        await _auditService.LogAsync(
            "notas_venta",
            "ANULAR",
            idNota,
            $"Nota de venta anulada. Motivo: {motivo}");
    }

    private async Task<List<SaleNoteEditDetailModel>> GetStoredSaleItemsAsync(int idNota, MySqlConnection connection, MySqlTransaction transaction)
    {
        const string sql = @"
SELECT id_producto, id_kit, es_kit,
       COALESCE(codigo_item, '') AS codigo_item,
       COALESCE(nombre_item, '') AS nombre_item,
       COALESCE(descripcion_item, '') AS descripcion_item,
       COALESCE(unidad_medida_item, 'unidad') AS unidad_medida_item,
       cantidad, precio_unitario, descuento
FROM detalle_nota_venta
WHERE id_nota = @id_nota
ORDER BY id_detalle;";

        List<SaleNoteEditDetailModel> items = new();
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id_nota", idNota);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SaleNoteEditDetailModel
            {
                IdProducto = reader.IsDBNull(reader.GetOrdinal("id_producto")) ? 0 : reader.GetInt32("id_producto"),
                IdKit = reader.IsDBNull(reader.GetOrdinal("id_kit")) ? null : reader.GetInt32("id_kit"),
                IsKit = reader.GetBoolean("es_kit"),
                Codigo = reader.GetString("codigo_item"),
                Producto = reader.GetString("nombre_item"),
                Descripcion = reader.GetString("descripcion_item"),
                UnidadMedida = reader.GetString("unidad_medida_item"),
                Cantidad = reader.GetInt32("cantidad"),
                PrecioUnitario = reader.GetDecimal("precio_unitario"),
                DescuentoMonto = reader.GetDecimal("descuento")
            });
        }

        return items;
    }

    private async Task ValidateStockAsync(IReadOnlyCollection<SaleNoteEditDetailModel> items, int warehouseId, MySqlConnection connection, MySqlTransaction transaction)
    {
        Dictionary<int, (int Quantity, string Name)> required = await BuildRequiredProductQuantitiesAsync(items, connection, transaction);
        if (required.Count == 0) return;

        string productIds = string.Join(",", required.Keys);
        string stockSql = $"SELECT i.id_producto, COALESCE(i.cantidad, 0) AS cantidad, p.nombre FROM inventario i INNER JOIN productos p ON p.id_producto = i.id_producto WHERE i.id_producto IN ({productIds}) AND i.id_almacen = @id_almacen FOR UPDATE;";
        await using var stockCommand = new MySqlCommand(stockSql, connection, transaction);
        stockCommand.Parameters.AddWithValue("@id_almacen", warehouseId);
        await using var reader = await stockCommand.ExecuteReaderAsync();
        var stockDict = new Dictionary<int, int>();
        while (await reader.ReadAsync())
        {
            stockDict[reader.GetInt32("id_producto")] = reader.GetInt32("cantidad");
        }
        await reader.CloseAsync();

        foreach ((int productId, (int quantity, string name)) in required)
        {
            int stockActual = stockDict.GetValueOrDefault(productId, 0);
            if (stockActual < quantity)
            {
                throw new InvalidOperationException($"Stock insuficiente para {name}. Disponible: {stockActual}.");
            }
        }
    }

    private async Task ApplyInventoryDeltaAsync(IEnumerable<SaleNoteEditDetailModel> items, int warehouseId, int sign, MySqlConnection connection, MySqlTransaction transaction)
    {
        Dictionary<int, (int Quantity, string Name)> required = await BuildRequiredProductQuantitiesAsync(items.ToList(), connection, transaction);
        if (required.Count == 0) return;

        string productIds = string.Join(",", required.Keys);
        string ensureRowSql = $@"
INSERT INTO inventario (id_producto, id_almacen, cantidad)
SELECT p.id_producto, @id_almacen, 0
FROM productos p
WHERE p.id_producto IN ({productIds})
  AND NOT EXISTS (
    SELECT 1 FROM inventario WHERE id_producto = p.id_producto AND id_almacen = @id_almacen
);";
        await using (var ensureRowCommand = new MySqlCommand(ensureRowSql, connection, transaction))
        {
            ensureRowCommand.Parameters.AddWithValue("@id_almacen", warehouseId);
            await ensureRowCommand.ExecuteNonQueryAsync();
        }

        var updateCases = new StringBuilder();
        foreach ((int productId, (int quantity, _)) in required)
        {
            updateCases.Append($"WHEN {productId} THEN cantidad + {sign * quantity} ");
        }
        string updateSql = $"UPDATE inventario SET cantidad = CASE id_producto {updateCases} END WHERE id_producto IN ({string.Join(",", required.Keys)}) AND id_almacen = @id_almacen;";
        await using var updateCommand = new MySqlCommand(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@id_almacen", warehouseId);
        await updateCommand.ExecuteNonQueryAsync();
    }

    private async Task InsertMovementsAsync(IEnumerable<SaleNoteEditDetailModel> items, int warehouseId, int userId, int saleId, MySqlConnection connection, MySqlTransaction transaction)
    {
        var movements = new List<(int ProductId, int Quantity, string Motivo)>();
        foreach (SaleNoteEditDetailModel item in items)
        {
            foreach ((int productId, int quantity, string componentName) in await ExpandItemComponentsAsync(item, connection, transaction))
            {
                string motivo = item.IsKit
                    ? $"Venta registrada | Kit: {item.Producto} | Componente: {componentName}"
                    : "Venta registrada";
                movements.Add((productId, quantity, motivo));
            }
        }

        if (movements.Count == 0) return;

        var values = new StringBuilder();
        for (int i = 0; i < movements.Count; i++)
        {
            var (productId, quantity, motivo) = movements[i];
            if (i > 0) values.Append(",");
            values.Append($"(@id_producto_{i}, @id_almacen_{i}, @id_usuario_{i}, 'salida', @cantidad_{i}, @motivo_{i}, @id_nota_{i})");
        }

        string movementSql = $@"
INSERT INTO movimientos_stock (id_producto, id_almacen, id_usuario, tipo, cantidad, motivo, referencia_id)
VALUES {values};";

        await using var movementCommand = new MySqlCommand(movementSql, connection, transaction);
        for (int i = 0; i < movements.Count; i++)
        {
            var (productId, quantity, motivo) = movements[i];
            movementCommand.Parameters.AddWithValue($"@id_producto_{i}", productId);
            movementCommand.Parameters.AddWithValue($"@id_almacen_{i}", warehouseId);
            movementCommand.Parameters.AddWithValue($"@id_usuario_{i}", userId);
            movementCommand.Parameters.AddWithValue($"@cantidad_{i}", quantity);
            movementCommand.Parameters.AddWithValue($"@motivo_{i}", motivo);
            movementCommand.Parameters.AddWithValue($"@id_nota_{i}", saleId);
        }
        await movementCommand.ExecuteNonQueryAsync();
    }

    private async Task InsertSaleDetailAsync(int saleId, SaleNoteEditDetailModel item, MySqlConnection connection, MySqlTransaction transaction)
    {
        const string sql = @"
INSERT INTO detalle_nota_venta
    (id_nota, id_producto, id_kit, es_kit, cantidad, precio_unitario, descuento, subtotal, codigo_item, nombre_item, descripcion_item, unidad_medida_item)
VALUES
    (@id_nota, @id_producto, @id_kit, @es_kit, @cantidad, @precio_unitario, @descuento, @subtotal, @codigo_item, @nombre_item, @descripcion_item, @unidad_medida_item);";

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id_nota", saleId);
        command.Parameters.AddWithValue("@id_producto", item.IsKit ? DBNull.Value : item.IdProducto);
        command.Parameters.AddWithValue("@id_kit", item.IsKit && item.IdKit.HasValue ? item.IdKit.Value : DBNull.Value);
        command.Parameters.AddWithValue("@es_kit", item.IsKit);
        command.Parameters.AddWithValue("@cantidad", item.Cantidad);
        command.Parameters.AddWithValue("@precio_unitario", item.PrecioUnitario);
        command.Parameters.AddWithValue("@descuento", item.DescuentoAplicado);
        command.Parameters.AddWithValue("@subtotal", item.Subtotal);
        command.Parameters.AddWithValue("@codigo_item", item.Codigo);
        command.Parameters.AddWithValue("@nombre_item", item.Producto);
        command.Parameters.AddWithValue("@descripcion_item", string.IsNullOrWhiteSpace(item.Descripcion) ? item.Producto : item.Descripcion);
        command.Parameters.AddWithValue("@unidad_medida_item", item.UnidadMedida);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Dictionary<int, (int Quantity, string Name)>> BuildRequiredProductQuantitiesAsync(IReadOnlyCollection<SaleNoteEditDetailModel> items, MySqlConnection connection, MySqlTransaction transaction)
    {
        Dictionary<int, (int Quantity, string Name)> required = new();
        foreach (SaleNoteEditDetailModel item in items)
        {
            foreach ((int productId, int quantity, string name) in await ExpandItemComponentsAsync(item, connection, transaction))
            {
                if (required.TryGetValue(productId, out var existing))
                {
                    required[productId] = (existing.Quantity + quantity, existing.Name);
                }
                else
                {
                    required[productId] = (quantity, name);
                }
            }
        }

        return required;
    }

    private async Task<List<(int ProductId, int Quantity, string Name)>> ExpandItemComponentsAsync(SaleNoteEditDetailModel item, MySqlConnection connection, MySqlTransaction transaction)
    {
        if (!item.IsKit)
        {
            return new List<(int ProductId, int Quantity, string Name)>
            {
                (item.IdProducto, item.Cantidad, item.Producto)
            };
        }

        if (!item.IdKit.HasValue)
        {
            throw new InvalidOperationException($"El item {item.Producto} esta marcado como kit pero no tiene ID de kit.");
        }

        const string sql = @"
SELECT kd.id_producto, kd.cantidad, p.nombre
FROM kit_detalle kd
INNER JOIN productos p ON p.id_producto = kd.id_producto
WHERE kd.id_kit = @id_kit;";

        List<(int ProductId, int Quantity, string Name)> components = new();
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id_kit", item.IdKit.Value);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            components.Add((
                reader.GetInt32("id_producto"),
                reader.GetInt32("cantidad") * item.Cantidad,
                reader.GetString("nombre")));
        }

        if (components.Count == 0)
        {
            throw new InvalidOperationException($"El kit {item.Producto} no tiene componentes configurados.");
        }

        return components;
    }

    private static SaleNoteEditDetailModel ToEditModel(SaleDetailDraft item)
    {
        return new SaleNoteEditDetailModel
        {
            IdProducto = item.IdProducto,
            IdKit = item.IdKit,
            IsKit = item.IsKit,
            Codigo = item.Codigo,
            Producto = item.Producto,
            Descripcion = item.Descripcion,
            UnidadMedida = item.UnidadMedida,
            Cantidad = item.Cantidad,
            PrecioUnitario = item.PrecioUnitario,
            DescuentoMonto = item.Descuento
        };
    }
}
