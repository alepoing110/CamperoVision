using System.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class ReportsRepository : IReportsRepository
{
    public async Task<List<SalesByDayReportItem>> GetSalesByDayAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null)
    {
        const string sql = @"
SELECT DATE(nv.fecha) AS fecha, COUNT(*) AS cantidad_notas, COALESCE(SUM(nv.total),0) AS total_vendido
FROM notas_venta nv
WHERE nv.fecha >= @from
  AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY DATE(nv.fecha)
ORDER BY DATE(nv.fecha);";

        var items = new List<SalesByDayReportItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        AddSaleFilters(command, from, to, warehouseId, userId, clientId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SalesByDayReportItem
            {
                Fecha = reader.GetDateTime("fecha"),
                CantidadNotas = reader.GetInt32("cantidad_notas"),
                TotalVendido = reader.GetDecimal("total_vendido")
            });
        }
        return items;
    }

    public async Task<List<TopProductReportItem>> GetTopProductsAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null)
    {
        const string sql = @"
SELECT p.codigo, p.nombre AS producto, SUM(d.cantidad) AS cantidad_vendida, SUM(d.subtotal) AS total_vendido
FROM detalle_nota_venta d
INNER JOIN notas_venta nv ON nv.id_nota = d.id_nota
INNER JOIN productos p ON p.id_producto = d.id_producto
WHERE nv.fecha >= @from
  AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY p.codigo, p.nombre
ORDER BY cantidad_vendida DESC, total_vendido DESC
LIMIT 10;";

        var items = new List<TopProductReportItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        AddSaleFilters(command, from, to, warehouseId, userId, clientId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new TopProductReportItem
            {
                Codigo = reader.GetStringSafe("codigo"),
                Producto = reader.GetStringSafe("producto", "Sin producto"),
                CantidadVendida = reader.GetInt32("cantidad_vendida"),
                TotalVendido = reader.GetDecimal("total_vendido")
            });
        }
        return items;
    }

    public async Task<List<ProductSalesReportItem>> GetProductSalesAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null)
    {
        const string sql = @"
SELECT p.codigo,
       p.nombre AS producto,
       SUM(d.cantidad) AS cantidad_vendida,
       AVG(d.precio_unitario) AS precio_promedio,
       SUM(d.descuento) AS descuento_total,
       SUM(d.subtotal) AS total_vendido
FROM detalle_nota_venta d
INNER JOIN notas_venta nv ON nv.id_nota = d.id_nota
INNER JOIN productos p ON p.id_producto = d.id_producto
WHERE nv.fecha >= @from
  AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY p.codigo, p.nombre
ORDER BY p.nombre;";

        var items = new List<ProductSalesReportItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        AddSaleFilters(command, from, to, warehouseId, userId, clientId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ProductSalesReportItem
            {
                Codigo = reader.GetStringSafe("codigo"),
                Producto = reader.GetStringSafe("producto", "Sin producto"),
                CantidadVendida = reader.GetInt32("cantidad_vendida"),
                PrecioPromedio = reader.GetDecimal("precio_promedio"),
                DescuentoTotal = reader.GetDecimal("descuento_total"),
                TotalVendido = reader.GetDecimal("total_vendido")
            });
        }
        return items;
    }

    public async Task<List<LowStockReportItem>> GetLowStockAsync(int? warehouseId = null)
    {
        const string sql = @"
SELECT a.nombre AS almacen, p.codigo, p.nombre AS producto, i.cantidad, p.stock_minimo
FROM inventario i
INNER JOIN productos p ON p.id_producto = i.id_producto
INNER JOIN almacenes a ON a.id_almacen = i.id_almacen
WHERE i.cantidad <= p.stock_minimo
  AND (@warehouseId IS NULL OR i.id_almacen = @warehouseId)
ORDER BY a.nombre, p.nombre;";

        var items = new List<LowStockReportItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@warehouseId", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new LowStockReportItem
            {
                Almacen = reader.GetStringSafe("almacen", "Sin almacen"),
                Codigo = reader.GetStringSafe("codigo"),
                Producto = reader.GetStringSafe("producto", "Sin producto"),
                Cantidad = reader.GetInt32("cantidad"),
                StockMinimo = reader.GetInt32("stock_minimo")
            });
        }
        return items;
    }

    public async Task<List<SalesByClientReportItem>> GetSalesByClientAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null)
    {
        const string sql = @"
SELECT 
    COALESCE(c.nombre, nv.nombre_comprador, 'Sin cliente') AS cliente,
    COALESCE(c.ci_nit, nv.ci_nit_comprador, '') AS ci_nit,
    COUNT(*) AS cantidad_notas,
    COALESCE(SUM(nv.total), 0) AS total_vendido
FROM notas_venta nv
LEFT JOIN clientes c ON c.id_cliente = nv.id_cliente
WHERE nv.fecha >= @from
  AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY cliente, ci_nit
ORDER BY total_vendido DESC;";

        var items = new List<SalesByClientReportItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        AddSaleFilters(command, from, to, warehouseId, userId, clientId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SalesByClientReportItem
            {
                Cliente = reader.GetStringSafe("cliente", "Sin cliente"),
                CiNit = reader.GetStringSafe("ci_nit"),
                CantidadNotas = reader.GetInt32("cantidad_notas"),
                TotalVendido = reader.GetDecimal("total_vendido")
            });
        }
        return items;
    }

    public async Task<List<InventoryMovementReportItem>> GetInventoryMovementsAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null)
    {
        const string sql = @"
SELECT 
    m.fecha,
    p.nombre AS producto,
    a.nombre AS almacen,
    m.tipo,
    m.cantidad,
    COALESCE(m.motivo, '') AS motivo
FROM movimientos_stock m
INNER JOIN productos p ON p.id_producto = m.id_producto
INNER JOIN almacenes a ON a.id_almacen = m.id_almacen
WHERE m.fecha >= @from
  AND m.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND (@warehouseId IS NULL OR m.id_almacen = @warehouseId)
  AND (@userId IS NULL OR m.id_usuario = @userId)
ORDER BY m.fecha DESC;";

        var items = new List<InventoryMovementReportItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@from", from.Date);
        command.Parameters.AddWithValue("@to", to.Date);
        command.Parameters.AddWithValue("@warehouseId", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@userId", userId.HasValue ? userId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new InventoryMovementReportItem
            {
                Fecha = reader.GetDateTime("fecha"),
                Producto = reader.GetStringSafe("producto", "Sin producto"),
                Almacen = reader.GetStringSafe("almacen", "Sin almacen"),
                Tipo = reader.GetStringSafe("tipo"),
                Cantidad = reader.GetInt32("cantidad"),
                Motivo = reader.GetStringSafe("motivo")
            });
        }
        return items;
    }

    private static void AddSaleFilters(MySqlCommand command, DateTime from, DateTime to, int? warehouseId, int? userId, int? clientId)
    {
        command.Parameters.AddWithValue("@from", from.Date);
        command.Parameters.AddWithValue("@to", to.Date);
        command.Parameters.AddWithValue("@warehouseId", warehouseId.HasValue ? warehouseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@userId", userId.HasValue ? userId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@clientId", clientId.HasValue ? clientId.Value : DBNull.Value);
    }

    public async Task<ReportsBundle> GetAllReportsAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null)
    {
        var bundle = new ReportsBundle();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using (var cmd = new MySqlCommand())
        {
            cmd.Connection = connection;
            AddSaleFilters(cmd, from, to, warehouseId, userId, clientId);
            cmd.CommandText = @"
SELECT DATE(nv.fecha) AS fecha, COUNT(*) AS cantidad_notas, COALESCE(SUM(nv.total),0) AS total_vendido
FROM notas_venta nv
WHERE nv.fecha >= @from AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY DATE(nv.fecha) ORDER BY DATE(nv.fecha);

SELECT p.codigo, p.nombre AS producto, SUM(d.cantidad) AS cantidad_vendida, SUM(d.subtotal) AS total_vendido
FROM detalle_nota_venta d
INNER JOIN notas_venta nv ON nv.id_nota = d.id_nota
INNER JOIN productos p ON p.id_producto = d.id_producto
WHERE nv.fecha >= @from AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY p.codigo, p.nombre
ORDER BY cantidad_vendida DESC, total_vendido DESC LIMIT 10;

SELECT p.codigo, p.nombre AS producto, SUM(d.cantidad) AS cantidad_vendida,
       AVG(d.precio_unitario) AS precio_promedio, SUM(d.descuento) AS descuento_total, SUM(d.subtotal) AS total_vendido
FROM detalle_nota_venta d
INNER JOIN notas_venta nv ON nv.id_nota = d.id_nota
INNER JOIN productos p ON p.id_producto = d.id_producto
WHERE nv.fecha >= @from AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY p.codigo, p.nombre ORDER BY p.nombre;

SELECT COALESCE(c.nombre, nv.nombre_comprador, 'Sin cliente') AS cliente,
       COALESCE(c.ci_nit, nv.ci_nit_comprador, '') AS ci_nit,
       COUNT(*) AS cantidad_notas, COALESCE(SUM(nv.total), 0) AS total_vendido
FROM notas_venta nv
LEFT JOIN clientes c ON c.id_cliente = nv.id_cliente
WHERE nv.fecha >= @from AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
  AND (@clientId IS NULL OR nv.id_cliente = @clientId)
GROUP BY cliente, ci_nit ORDER BY total_vendido DESC;

SELECT a.nombre AS almacen, p.codigo, p.nombre AS producto, i.cantidad, p.stock_minimo
FROM inventario i
INNER JOIN productos p ON p.id_producto = i.id_producto
INNER JOIN almacenes a ON a.id_almacen = i.id_almacen
WHERE i.cantidad <= p.stock_minimo
  AND (@warehouseId IS NULL OR i.id_almacen = @warehouseId)
ORDER BY a.nombre, p.nombre;

SELECT m.fecha, p.nombre AS producto, a.nombre AS almacen, m.tipo, m.cantidad, COALESCE(m.motivo, '') AS motivo
FROM movimientos_stock m
INNER JOIN productos p ON p.id_producto = m.id_producto
INNER JOIN almacenes a ON a.id_almacen = m.id_almacen
WHERE m.fecha >= @from AND m.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND (@warehouseId IS NULL OR m.id_almacen = @warehouseId)
  AND (@userId IS NULL OR m.id_usuario = @userId)
ORDER BY m.fecha DESC;";

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                bundle.SalesByDay.Add(new SalesByDayReportItem
                {
                    Fecha = reader.GetDateTime("fecha"),
                    CantidadNotas = reader.GetInt32("cantidad_notas"),
                    TotalVendido = reader.GetDecimal("total_vendido")
                });
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    bundle.TopProducts.Add(new TopProductReportItem
                    {
                        Codigo = reader.GetStringSafe("codigo"),
                        Producto = reader.GetStringSafe("producto", "Sin producto"),
                        CantidadVendida = reader.GetInt32("cantidad_vendida"),
                        TotalVendido = reader.GetDecimal("total_vendido")
                    });
                }
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    bundle.ProductSales.Add(new ProductSalesReportItem
                    {
                        Codigo = reader.GetStringSafe("codigo"),
                        Producto = reader.GetStringSafe("producto", "Sin producto"),
                        CantidadVendida = reader.GetInt32("cantidad_vendida"),
                        PrecioPromedio = reader.GetDecimal("precio_promedio"),
                        DescuentoTotal = reader.GetDecimal("descuento_total"),
                        TotalVendido = reader.GetDecimal("total_vendido")
                    });
                }
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    bundle.SalesByClient.Add(new SalesByClientReportItem
                    {
                        Cliente = reader.GetStringSafe("cliente", "Sin cliente"),
                        CiNit = reader.GetStringSafe("ci_nit"),
                        CantidadNotas = reader.GetInt32("cantidad_notas"),
                        TotalVendido = reader.GetDecimal("total_vendido")
                    });
                }
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    bundle.LowStock.Add(new LowStockReportItem
                    {
                        Almacen = reader.GetStringSafe("almacen", "Sin almacen"),
                        Codigo = reader.GetStringSafe("codigo"),
                        Producto = reader.GetStringSafe("producto", "Sin producto"),
                        Cantidad = reader.GetInt32("cantidad"),
                        StockMinimo = reader.GetInt32("stock_minimo")
                    });
                }
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    bundle.InventoryMovements.Add(new InventoryMovementReportItem
                    {
                        Fecha = reader.GetDateTime("fecha"),
                        Producto = reader.GetStringSafe("producto", "Sin producto"),
                        Almacen = reader.GetStringSafe("almacen", "Sin almacen"),
                        Tipo = reader.GetStringSafe("tipo"),
                        Cantidad = reader.GetInt32("cantidad"),
                        Motivo = reader.GetStringSafe("motivo")
                    });
                }
            }
        }

        return bundle;
    }
}
