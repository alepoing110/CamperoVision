using System.Text.Json;
using CamperoDesktop.Data;
using Serilog;

namespace CamperoDesktop.Services;

public interface IAuditService
{
    Task LogAsync(string tabla, string accion, int? registroId, string descripcion, object? datosAnteriores = null, object? datosNuevos = null);
}

public class AuditService : IAuditService
{
    private readonly AuditRepository _repository;
    private readonly ISessionService _sessionService;

    public AuditService(AuditRepository repository, ISessionService sessionService)
    {
        _repository = repository;
        _sessionService = sessionService;
    }

    public async Task LogAsync(string tabla, string accion, int? registroId, string descripcion, object? datosAnteriores = null, object? datosNuevos = null)
    {
        try
        {
            string usuario = _sessionService.CurrentUser?.Nombre ?? "Sistema";
            string? jsonAnterior = datosAnteriores != null ? Serialize(datosAnteriores) : null;
            string? jsonNuevo = datosNuevos != null ? Serialize(datosNuevos) : null;

            await _repository.InsertAsync(usuario, tabla, accion, registroId, descripcion, jsonAnterior, jsonNuevo);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error al registrar auditoria en {Tabla} - {Accion}", tabla, accion);
        }
    }

    private static string Serialize(object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return obj.ToString() ?? string.Empty;
        }
    }
}
