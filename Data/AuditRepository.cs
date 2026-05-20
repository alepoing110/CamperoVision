using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class AuditRepository
{
    private bool _schemaReady;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    public async Task EnsureSchemaAsync()
    {
        if (_schemaReady) return;
        await _schemaLock.WaitAsync();
        try
        {
            if (_schemaReady) return;

            const string sql = @"
CREATE TABLE IF NOT EXISTS audit_logs (
    id BIGINT NOT NULL AUTO_INCREMENT,
    fecha DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    usuario VARCHAR(100) NOT NULL,
    tabla VARCHAR(100) NOT NULL,
    accion VARCHAR(50) NOT NULL,
    registro_id INT NULL,
    descripcion TEXT NOT NULL,
    datos_anteriores JSON NULL,
    datos_nuevos JSON NULL,
    ip_origen VARCHAR(45) NOT NULL DEFAULT '',
    PRIMARY KEY (id),
    INDEX idx_fecha (fecha),
    INDEX idx_usuario (usuario),
    INDEX idx_tabla (tabla),
    INDEX idx_accion (accion)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using var connection = DbConnectionFactory.CreateConnection();
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    public async Task InsertAsync(string usuario, string tabla, string accion, int? registroId, string descripcion, string? datosAnteriores, string? datosNuevos)
    {
        await EnsureSchemaAsync();

        const string sql = @"
INSERT INTO audit_logs (usuario, tabla, accion, registro_id, descripcion, datos_anteriores, datos_nuevos)
VALUES (@usuario, @tabla, @accion, @registroId, @descripcion, @datosAnteriores, @datosNuevos);";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@usuario", usuario);
        command.Parameters.AddWithValue("@tabla", tabla);
        command.Parameters.AddWithValue("@accion", accion);
        command.Parameters.AddWithValue("@registroId", registroId.HasValue ? registroId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@descripcion", descripcion);
        command.Parameters.AddWithValue("@datosAnteriores", datosAnteriores != null ? datosAnteriores : DBNull.Value);
        command.Parameters.AddWithValue("@datosNuevos", datosNuevos != null ? datosNuevos : DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditLog>> GetAsync(string search = "", string? tabla = null, string? accion = null, DateTime? desde = null, DateTime? hasta = null, int page = 1, int pageSize = 50)
    {
        await EnsureSchemaAsync();

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClauses.Add("(usuario LIKE CONCAT('%', @search, '%') OR descripcion LIKE CONCAT('%', @search, '%'))");
            parameters["@search"] = search.Trim();
        }
        if (!string.IsNullOrWhiteSpace(tabla))
        {
            whereClauses.Add("tabla = @tabla");
            parameters["@tabla"] = tabla;
        }
        if (!string.IsNullOrWhiteSpace(accion))
        {
            whereClauses.Add("accion = @accion");
            parameters["@accion"] = accion;
        }
        if (desde.HasValue)
        {
            whereClauses.Add("fecha >= @desde");
            parameters["@desde"] = desde.Value;
        }
        if (hasta.HasValue)
        {
            whereClauses.Add("fecha <= @hasta");
            parameters["@hasta"] = hasta.Value.AddDays(1).Date;
        }

        string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : string.Empty;

        string sql = $@"
SELECT id, fecha, usuario, tabla, accion, registro_id, descripcion, datos_anteriores, datos_nuevos, ip_origen
FROM audit_logs
{whereSql}
ORDER BY fecha DESC
LIMIT @offset, @pageSize;";

        var items = new List<AuditLog>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        foreach (var kvp in parameters)
        {
            command.Parameters.AddWithValue(kvp.Key, kvp.Value);
        }
        command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("@pageSize", pageSize);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new AuditLog
            {
                Id = reader.GetInt64("id"),
                Fecha = reader.GetDateTime("fecha"),
                Usuario = reader.GetString("usuario"),
                Tabla = reader.GetString("tabla"),
                Accion = reader.GetString("accion"),
                RegistroId = reader.GetNullableInt32("registro_id"),
                Descripcion = reader.GetString("descripcion"),
                DatosAnteriores = reader.IsColumnNull("datos_anteriores") ? null : reader.GetString("datos_anteriores"),
                DatosNuevos = reader.IsColumnNull("datos_nuevos") ? null : reader.GetString("datos_nuevos"),
                IpOrigen = reader.GetString("ip_origen")
            });
        }

        return items;
    }

    public async Task<int> GetTotalCountAsync(string search = "", string? tabla = null, string? accion = null, DateTime? desde = null, DateTime? hasta = null)
    {
        await EnsureSchemaAsync();

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClauses.Add("(usuario LIKE CONCAT('%', @search, '%') OR descripcion LIKE CONCAT('%', @search, '%'))");
            parameters["@search"] = search.Trim();
        }
        if (!string.IsNullOrWhiteSpace(tabla))
        {
            whereClauses.Add("tabla = @tabla");
            parameters["@tabla"] = tabla;
        }
        if (!string.IsNullOrWhiteSpace(accion))
        {
            whereClauses.Add("accion = @accion");
            parameters["@accion"] = accion;
        }
        if (desde.HasValue)
        {
            whereClauses.Add("fecha >= @desde");
            parameters["@desde"] = desde.Value;
        }
        if (hasta.HasValue)
        {
            whereClauses.Add("fecha <= @hasta");
            parameters["@hasta"] = hasta.Value.AddDays(1).Date;
        }

        string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : string.Empty;

        string sql = $"SELECT COUNT(*) FROM audit_logs {whereSql};";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        foreach (var kvp in parameters)
        {
            command.Parameters.AddWithValue(kvp.Key, kvp.Value);
        }
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<List<string>> GetDistinctTablesAsync()
    {
        await EnsureSchemaAsync();
        var tables = new List<string>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand("SELECT DISTINCT tabla FROM audit_logs ORDER BY tabla;", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    public async Task<List<string>> GetDistinctActionsAsync()
    {
        await EnsureSchemaAsync();
        var actions = new List<string>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand("SELECT DISTINCT accion FROM audit_logs ORDER BY accion;", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actions.Add(reader.GetString(0));
        }
        return actions;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate)
    {
        await EnsureSchemaAsync();
        const string sql = "DELETE FROM audit_logs WHERE fecha < @cutoff;";
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@cutoff", cutoffDate);
        return await command.ExecuteNonQueryAsync();
    }
}
