using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class CajaRepository
{
    /// <summary>
    /// Obtiene la sesión de caja ABIERTA para el usuario actual en la caja más cercana al almacén
    /// </summary>
    public async Task<SesionCajaInfo?> GetSesionAbiertaAsync(int idUsuario, int idAlmacen)
    {
        const string sql = @"
SELECT s.id_sesion, s.id_caja, c.nombre as caja_nombre, s.apertura, s.monto_inicial, s.estado
FROM sesiones_caja s
INNER JOIN cajas c ON c.id_caja = s.id_caja
WHERE s.id_usuario = @idUsuario 
  AND s.estado = 'abierta'
  AND c.id_almacen = @idAlmacen
ORDER BY s.apertura DESC 
LIMIT 1;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idUsuario", idUsuario);
        command.Parameters.AddWithValue("@idAlmacen", idAlmacen);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SesionCajaInfo
            {
                IdSesion = reader.GetInt32("id_sesion"),
                IdCaja = reader.GetInt32("id_caja"),
                NombreCaja = reader.GetString("caja_nombre"),
                Apertura = reader.GetDateTime("apertura"),
                MontoInicial = reader.GetDecimal("monto_inicial"),
                Estado = reader.GetString("estado")
            };
        }
        return null;
    }

    /// <summary>
    /// Registra pago para nota de venta vinculada a sesion de caja activa
    /// </summary>
    public async Task<bool> RegistrarPagoAsync(int idNota, int idSesion, string metodo, decimal monto, string? referencia = null)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Validar que la sesion existe y esta abierta
            const string checkSesion = "SELECT estado FROM sesiones_caja WHERE id_sesion = @idSesion LIMIT 1 FOR UPDATE;";
            await using var checkCmd = new MySqlCommand(checkSesion, connection, transaction);
            checkCmd.Parameters.AddWithValue("@idSesion", idSesion);
            object? estadoObj = await checkCmd.ExecuteScalarAsync();
            if (estadoObj == null || estadoObj.ToString() != "abierta")
            {
                throw new InvalidOperationException("La sesion de caja no existe o esta cerrada.");
            }

            // Validar que la nota existe y no tiene pagos previos
            const string checkNota = "SELECT estado FROM notas_venta WHERE id_nota = @idNota LIMIT 1;";
            await using var notaCmd = new MySqlCommand(checkNota, connection, transaction);
            notaCmd.Parameters.AddWithValue("@idNota", idNota);
            object? notaEstado = await notaCmd.ExecuteScalarAsync();
            if (notaEstado == null)
            {
                throw new InvalidOperationException("La nota de venta no existe.");
            }

            // Insertar el pago
            const string insertPago = @"
INSERT INTO pagos (id_nota, id_sesion, metodo, monto, referencia, fecha)
VALUES (@idNota, @idSesion, @metodo, @monto, @referencia, NOW());";
            await using var insertCmd = new MySqlCommand(insertPago, connection, transaction);
            insertCmd.Parameters.AddWithValue("@idNota", idNota);
            insertCmd.Parameters.AddWithValue("@idSesion", idSesion);
            insertCmd.Parameters.AddWithValue("@metodo", metodo);
            insertCmd.Parameters.AddWithValue("@monto", monto);
            insertCmd.Parameters.AddWithValue("@referencia", (object?)referencia ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public class SesionCajaInfo
{
    public int IdSesion { get; set; }
    public int IdCaja { get; set; }
    public string NombreCaja { get; set; } = string.Empty;
    public DateTime Apertura { get; set; }
    public decimal MontoInicial { get; set; }
    public string Estado { get; set; } = string.Empty;
}
