using CamperoDesktop.Data;
using CamperoDesktop.Models;

namespace CamperoDesktop.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;

    public InventoryService(IInventoryRepository repository)
    {
        _repository = repository;
    }

    public int WarehouseId { get; set; }
    public int UserId { get; set; }
    public string DefaultReason { get; set; } = "Movimiento manual";

    public async Task RegistrarMovimiento(int productoId, int cantidad, decimal precio, string tipo)
    {
        if (WarehouseId <= 0)
        {
            throw new InvalidOperationException("Selecciona un almacen antes de registrar un movimiento.");
        }

        if (UserId <= 0)
        {
            throw new InvalidOperationException("No se encontro el usuario actual para registrar el movimiento.");
        }

        if (productoId <= 0)
        {
            throw new InvalidOperationException("Selecciona un producto valido.");
        }

        if (cantidad <= 0)
        {
            throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
        }

        if (precio < 0)
        {
            throw new InvalidOperationException("El precio no puede ser negativo.");
        }

        string normalizedType = tipo.Trim().ToLowerInvariant();
        if (normalizedType is not ("entrada" or "salida"))
        {
            throw new InvalidOperationException("El tipo de movimiento debe ser 'entrada' o 'salida'.");
        }

        int stockActual = await _repository.GetCurrentStockAsync(productoId, WarehouseId);
        if (normalizedType == "salida" && stockActual < cantidad)
        {
            throw new InvalidOperationException($"Stock insuficiente. Disponible actual: {stockActual}.");
        }

        await _repository.RegisterMovementAsync(new InventoryMovementRequest
        {
            IdProducto = productoId,
            IdAlmacen = WarehouseId,
            IdUsuario = UserId,
            Cantidad = cantidad,
            Precio = precio,
            Tipo = normalizedType,
            Motivo = string.IsNullOrWhiteSpace(DefaultReason) ? $"Movimiento {normalizedType}" : DefaultReason
        });
    }
}
