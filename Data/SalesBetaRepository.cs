using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class SalesBetaRepository
{
    public async Task<List<SalesBetaReportItem>> GetSalesAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null)
    {
        const string sql = @"
SELECT p.codigo,
       p.nombre AS producto,
       a.nombre AS almacen,
       SUM(d.cantidad) AS cantidad_vendida,
       AVG(d.precio_unitario) AS precio_venta_promedio,
       p.precio_compra AS precio_compra_referencia,
       SUM(d.subtotal) AS total_vendido,
       SUM(d.cantidad * p.precio_compra) AS costo_total
FROM detalle_nota_venta d
INNER JOIN notas_venta nv ON nv.id_nota = d.id_nota
INNER JOIN productos p ON p.id_producto = d.id_producto
INNER JOIN almacenes a ON a.id_almacen = nv.id_almacen
WHERE nv.fecha >= @from
  AND nv.fecha < DATE_ADD(@to, INTERVAL 1 DAY)
  AND nv.estado = 'completada'
  AND (@warehouseId IS NULL OR nv.id_almacen = @warehouseId)
  AND (@userId IS NULL OR nv.id_usuario = @userId)
GROUP BY p.codigo, p.nombre, a.nombre, p.precio_compra
ORDER BY SUM(d.subtotal - (d.cantidad * p.precio_compra)) DESC, p.nombre;";

        List<SalesBetaReportItem> items = new();
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
            items.Add(new SalesBetaReportItem
            {
                Codigo = GetStringSafe(reader, "codigo"),
                Producto = GetStringSafe(reader, "producto", "Sin producto"),
                Almacen = GetStringSafe(reader, "almacen", "Sin almacen"),
                CantidadVendida = reader.GetInt32("cantidad_vendida"),
                PrecioVentaPromedio = reader.GetDecimal("precio_venta_promedio"),
                PrecioCompraReferencia = reader.GetDecimal("precio_compra_referencia"),
                TotalVendido = reader.GetDecimal("total_vendido"),
                CostoTotal = reader.GetDecimal("costo_total")
            });
        }

        return items;
    }

    private static string GetStringSafe(MySqlDataReader reader, string columnName, string defaultValue = "")
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
    }
}
