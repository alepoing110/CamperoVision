using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class DashboardRepository
{
    public async Task<DashboardSummary> GetSummaryAsync()
    {
        const string sql = @"
SELECT
    (SELECT COUNT(*) FROM productos WHERE activo = 1) AS TotalProductos,
    (SELECT COUNT(*) FROM clientes WHERE activo = 1) AS TotalClientes,
    (SELECT COUNT(*) FROM almacenes WHERE activo = 1) AS TotalAlmacenes,
    (SELECT COUNT(*) FROM notas_venta) AS TotalNotasVenta,
    (SELECT COALESCE(SUM(total), 0) FROM notas_venta WHERE estado = 'completada' AND DATE(fecha) = CURDATE()) AS VentasHoy,
    (SELECT COALESCE(SUM(total), 0) FROM notas_venta WHERE estado = 'completada' AND YEAR(fecha) = YEAR(CURDATE()) AND MONTH(fecha) = MONTH(CURDATE())) AS VentasMes,
    (SELECT COUNT(*) FROM notas_venta WHERE DATE(fecha) = CURDATE()) AS NotasEmitidasHoy;";

        await using MySqlConnection connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using MySqlCommand command = new(sql, connection);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync();

        DashboardSummary summary = new();

        if (await reader.ReadAsync())
        {
            summary.TotalProductos = reader.GetInt32("TotalProductos");
            summary.TotalClientes = reader.GetInt32("TotalClientes");
            summary.TotalAlmacenes = reader.GetInt32("TotalAlmacenes");
            summary.TotalNotasVenta = reader.GetInt32("TotalNotasVenta");
            summary.VentasHoy = reader.GetDecimal("VentasHoy");
            summary.VentasMes = reader.GetDecimal("VentasMes");
            summary.NotasEmitidasHoy = reader.GetInt32("NotasEmitidasHoy");
        }

        return summary;
    }

    public async Task<List<LowStockReportItem>> GetLowStockProductsAsync(int limit = 12)
    {
        const string sql = @"
SELECT a.nombre AS almacen,
       p.codigo,
       p.nombre AS producto,
       i.cantidad,
       p.stock_minimo
FROM inventario i
INNER JOIN productos p ON p.id_producto = i.id_producto
INNER JOIN almacenes a ON a.id_almacen = i.id_almacen
WHERE p.activo = 1
  AND a.activo = 1
  AND i.cantidad <= p.stock_minimo
ORDER BY
    (p.stock_minimo - i.cantidad) DESC,
    p.nombre
LIMIT @limit;";

        var items = new List<LowStockReportItem>();
        await using MySqlConnection connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using MySqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@limit", limit);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new LowStockReportItem
            {
                Almacen = reader.GetString("almacen"),
                Codigo = reader.GetString("codigo"),
                Producto = reader.GetString("producto"),
                Cantidad = reader.GetInt32("cantidad"),
                StockMinimo = reader.GetInt32("stock_minimo")
            });
        }

        return items;
    }
}
