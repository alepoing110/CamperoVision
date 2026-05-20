using CamperoDesktop.Models;

namespace CamperoDesktop.Data;

public interface IReportsRepository
{
    Task<List<SalesByDayReportItem>> GetSalesByDayAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null);
    Task<List<TopProductReportItem>> GetTopProductsAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null);
    Task<List<ProductSalesReportItem>> GetProductSalesAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null);
    Task<List<LowStockReportItem>> GetLowStockAsync(int? warehouseId = null);
    Task<List<SalesByClientReportItem>> GetSalesByClientAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null);
    Task<List<InventoryMovementReportItem>> GetInventoryMovementsAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null);
    Task<ReportsBundle> GetAllReportsAsync(DateTime from, DateTime to, int? warehouseId = null, int? userId = null, int? clientId = null);
}

public class ReportsBundle
{
    public List<SalesByDayReportItem> SalesByDay { get; set; } = new();
    public List<TopProductReportItem> TopProducts { get; set; } = new();
    public List<ProductSalesReportItem> ProductSales { get; set; } = new();
    public List<LowStockReportItem> LowStock { get; set; } = new();
    public List<SalesByClientReportItem> SalesByClient { get; set; } = new();
    public List<InventoryMovementReportItem> InventoryMovements { get; set; } = new();
}
