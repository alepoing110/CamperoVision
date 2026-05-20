using CamperoDesktop.Models;

namespace CamperoDesktop.Data;

public interface IInventoryRepository
{
    Task<List<WarehouseItem>> GetWarehousesAsync();
    Task<List<ProductOption>> GetProductsAsync(int? warehouseId = null, string search = "", bool includeKits = false);
    Task<List<InventoryListItem>> GetAllAsync(int? warehouseId = null, string search = "", int page = 1, int pageSize = 200);
    Task<int> GetCurrentStockAsync(int productId, int warehouseId);
    Task<List<InventoryMovementItem>> GetKardexAsync(int? warehouseId = null, int? productId = null, int limit = 100);
    Task RegisterMovementAsync(InventoryMovementRequest request);
    Task AdjustStockAsync(InventoryAdjustmentRequest request);
}
