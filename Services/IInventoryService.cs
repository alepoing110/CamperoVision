namespace CamperoDesktop.Services;

public interface IInventoryService
{
    int WarehouseId { get; set; }
    int UserId { get; set; }
    string DefaultReason { get; set; }
    Task RegistrarMovimiento(int productoId, int cantidad, decimal precio, string tipo);
}
