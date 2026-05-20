using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using MySqlConnector;

namespace CamperoDesktop.Data;

public class UserRepository
{
    private readonly IAuditService _auditService;

    public UserRepository(IAuditService auditService)
    {
        _auditService = auditService;
    }
    public async Task<List<AppUserListItem>> GetAllAsync()
    {
        const string sql = @"SELECT id_usuario, nombre, usuario, rol, activo, creado_en FROM usuarios ORDER BY nombre;";
        var items = new List<AppUserListItem>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new AppUserListItem
            {
                IdUsuario = reader.GetInt32("id_usuario"),
                Nombre = reader.GetString("nombre"),
                Usuario = reader.GetString("usuario"),
                Rol = reader.GetString("rol"),
                Activo = reader.GetBoolean("activo"),
                CreadoEn = reader.GetDateTime("creado_en")
            });
        }

        return items;
    }

    public async Task SaveAsync(UserUpsertModel model)
    {
        bool isUpdate = model.IdUsuario != 0;
        object? datosAnteriores = null;

        if (isUpdate)
        {
            await using var conn = DbConnectionFactory.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT nombre, usuario, rol, activo FROM usuarios WHERE id_usuario = @id;", conn);
            cmd.Parameters.AddWithValue("@id", model.IdUsuario);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                datosAnteriores = new
                {
                    nombre = reader.GetString("nombre"),
                    usuario = reader.GetString("usuario"),
                    rol = reader.GetString("rol"),
                    activo = reader.GetBoolean("activo")
                };
            }
        }

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        bool updatePassword = !string.IsNullOrWhiteSpace(model.Password);
        string sql;

        if (model.IdUsuario == 0)
        {
            sql = @"INSERT INTO usuarios (nombre, usuario, password_hash, rol, activo)
                    VALUES (@nombre, @usuario, SHA2(@password, 256), @rol, @activo);";
        }
        else if (updatePassword)
        {
            sql = @"UPDATE usuarios SET nombre=@nombre, usuario=@usuario, password_hash=SHA2(@password, 256), rol=@rol, activo=@activo WHERE id_usuario=@id_usuario;";
        }
        else
        {
            sql = @"UPDATE usuarios SET nombre=@nombre, usuario=@usuario, rol=@rol, activo=@activo WHERE id_usuario=@id_usuario;";
        }

        await using var command = new MySqlCommand(sql, connection);
        if (model.IdUsuario != 0)
        {
            command.Parameters.AddWithValue("@id_usuario", model.IdUsuario);
        }

        command.Parameters.AddWithValue("@nombre", model.Nombre);
        command.Parameters.AddWithValue("@usuario", model.Usuario);
        command.Parameters.AddWithValue("@rol", model.Rol);
        command.Parameters.AddWithValue("@activo", model.Activo);
        if (model.IdUsuario == 0 || updatePassword)
        {
            command.Parameters.AddWithValue("@password", model.Password);
        }

        await command.ExecuteNonQueryAsync();

        var datosNuevos = new
        {
            nombre = model.Nombre,
            usuario = model.Usuario,
            rol = model.Rol,
            activo = model.Activo,
            password_changed = updatePassword
        };

        await _auditService.LogAsync(
            "usuarios",
            isUpdate ? "UPDATE" : "CREATE",
            model.IdUsuario,
            isUpdate ? $"Usuario actualizado: {model.Usuario}" : $"Usuario creado: {model.Usuario}",
            datosAnteriores,
            datosNuevos);
    }
}
