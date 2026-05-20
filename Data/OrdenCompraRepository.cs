using CamperoDesktop.Models;
using MySqlConnector;
using System.Data;

namespace CamperoDesktop.Data;

public class OrdenCompraRepository
{
    public async Task<List<OrdenCompraListItem>> GetAllAsync()
    {
        const string sql = @"
SELECT 
  oc.id_orden,
  p.nombre AS proveedor,
  p.nit,
  a.nombre AS almacen,
  oc.fecha,
  oc.estado,
  oc.total
FROM ordenes_compra oc
INNER JOIN proveedores p ON p.id_proveedor = oc.id_proveedor
INNER JOIN almacenes a ON a.id_almacen = oc.id_almacen
ORDER BY oc.fecha DESC";

        var items = new List<OrdenCompraListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new OrdenCompraListItem
            {
                IdOrden = reader.GetInt32("id_orden"),
                Proveedor = reader.GetString("proveedor"),
                Nit = reader.IsDBNull("nit") ? "" : reader.GetString("nit"),
                Almacen = reader.GetString("almacen"),
                Fecha = reader.GetDateTime("fecha").ToString("dd/MM/yyyy"),
                Estado = reader.GetString("estado"),
                Total = reader.GetDecimal("total")
            });
        }
        return items;
    }

    public async Task<int> CreateAsync(PurchaseCreateRequest request)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Agrega al menos un producto a la orden de compra.");
        }

        decimal total = request.Items.Sum(x => x.Subtotal);

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        const string orderSql = @"
INSERT INTO ordenes_compra (id_proveedor, id_usuario, id_almacen, fecha, estado, total, observaciones)
VALUES (@id_proveedor, @id_usuario, @id_almacen, @fecha, @estado, @total, @observaciones);";

        await using var orderCommand = new MySqlCommand(orderSql, connection, transaction);
        orderCommand.Parameters.AddWithValue("@id_proveedor", request.IdProveedor);
        orderCommand.Parameters.AddWithValue("@id_usuario", request.IdUsuario);
        orderCommand.Parameters.AddWithValue("@id_almacen", request.IdAlmacen);
        orderCommand.Parameters.AddWithValue("@fecha", request.Fecha.Date);
        orderCommand.Parameters.AddWithValue("@estado", request.Estado);
        orderCommand.Parameters.AddWithValue("@total", total);
        orderCommand.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones.Trim());
        await orderCommand.ExecuteNonQueryAsync();

        int orderId = (int)orderCommand.LastInsertedId;

        const string detailSql = @"
INSERT INTO detalle_orden_compra (id_orden, id_producto, cantidad, precio_unitario, subtotal)
VALUES (@id_orden, @id_producto, @cantidad, @precio_unitario, @subtotal);";

        foreach (DetalleOrdenCompraDraft item in request.Items)
        {
            await using var detailCommand = new MySqlCommand(detailSql, connection, transaction);
            detailCommand.Parameters.AddWithValue("@id_orden", orderId);
            detailCommand.Parameters.AddWithValue("@id_producto", item.IdProducto);
            detailCommand.Parameters.AddWithValue("@cantidad", item.Cantidad);
            detailCommand.Parameters.AddWithValue("@precio_unitario", item.PrecioUnitario);
            detailCommand.Parameters.AddWithValue("@subtotal", item.Subtotal);
            await detailCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return orderId;
    }

    public async Task ReceiveAsync(int idOrden, int idUsuario)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        const string orderSql = @"
SELECT id_almacen, estado
FROM ordenes_compra
WHERE id_orden = @id_orden
LIMIT 1
FOR UPDATE;";

        await using var orderCommand = new MySqlCommand(orderSql, connection, transaction);
        orderCommand.Parameters.AddWithValue("@id_orden", idOrden);
        await using var orderReader = await orderCommand.ExecuteReaderAsync();

        if (!await orderReader.ReadAsync())
        {
            throw new InvalidOperationException("La orden de compra no existe.");
        }

        int warehouseId = orderReader.GetInt32("id_almacen");
        string status = orderReader.GetString("estado");
        await orderReader.CloseAsync();

        if (status == "recibida")
        {
            throw new InvalidOperationException("La orden de compra ya fue recibida.");
        }

        if (status == "cancelada")
        {
            throw new InvalidOperationException("No puedes recibir una orden cancelada.");
        }

        const string detailSql = @"
SELECT d.id_producto, d.cantidad, d.precio_unitario, p.nombre
FROM detalle_orden_compra d
INNER JOIN productos p ON p.id_producto = d.id_producto
WHERE d.id_orden = @id_orden;";

        var details = new List<(int ProductId, int Quantity, decimal Price, string ProductName)>();
        await using var detailCommand = new MySqlCommand(detailSql, connection, transaction);
        detailCommand.Parameters.AddWithValue("@id_orden", idOrden);
        await using var detailReader = await detailCommand.ExecuteReaderAsync();
        while (await detailReader.ReadAsync())
        {
            details.Add((
                detailReader.GetInt32("id_producto"),
                detailReader.GetInt32("cantidad"),
                detailReader.GetDecimal("precio_unitario"),
                detailReader.GetString("nombre")));
        }
        await detailReader.CloseAsync();

        if (details.Count > 0)
        {
            var inventoryValues = new System.Text.StringBuilder();
            var movementValues = new System.Text.StringBuilder();
            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                if (i > 0)
                {
                    inventoryValues.Append(",");
                    movementValues.Append(",");
                }
                inventoryValues.Append($"(@id_producto_{i}, @id_almacen, @cantidad_{i})");
                movementValues.Append($"(@id_producto_{i}, @id_almacen, @id_usuario, 'entrada', @cantidad_{i}, @motivo_{i}, @referencia_id_{i})");
            }

            string inventorySql = $@"
INSERT INTO inventario (id_producto, id_almacen, cantidad)
VALUES {inventoryValues}
ON DUPLICATE KEY UPDATE cantidad = cantidad + VALUES(cantidad);";

            await using var inventoryCommand = new MySqlCommand(inventorySql, connection, transaction);
            inventoryCommand.Parameters.AddWithValue("@id_almacen", warehouseId);
            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                inventoryCommand.Parameters.AddWithValue($"@id_producto_{i}", detail.ProductId);
                inventoryCommand.Parameters.AddWithValue($"@cantidad_{i}", detail.Quantity);
            }
            await inventoryCommand.ExecuteNonQueryAsync();

            string movementSql = $@"
INSERT INTO movimientos_stock (id_producto, id_almacen, id_usuario, tipo, cantidad, motivo, referencia_id)
VALUES {movementValues};";

            await using var movementCommand = new MySqlCommand(movementSql, connection, transaction);
            movementCommand.Parameters.AddWithValue("@id_almacen", warehouseId);
            movementCommand.Parameters.AddWithValue("@id_usuario", idUsuario);
            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                movementCommand.Parameters.AddWithValue($"@id_producto_{i}", detail.ProductId);
                movementCommand.Parameters.AddWithValue($"@cantidad_{i}", detail.Quantity);
                movementCommand.Parameters.AddWithValue($"@motivo_{i}", $"Recepcion OC #{idOrden} | Precio: {detail.Price:N2}");
                movementCommand.Parameters.AddWithValue($"@referencia_id_{i}", idOrden);
            }
            await movementCommand.ExecuteNonQueryAsync();
        }

        const string updateStatusSql = "UPDATE ordenes_compra SET estado = 'recibida' WHERE id_orden = @id_orden;";
        await using var updateCommand = new MySqlCommand(updateStatusSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@id_orden", idOrden);
        await updateCommand.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
    }
}
