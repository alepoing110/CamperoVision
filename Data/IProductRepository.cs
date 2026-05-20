using CamperoDesktop.Models;

namespace CamperoDesktop.Data;

public interface IProductRepository
{
    Task<List<ProductListItem>> GetAllAsync(string search = "", int page = 1, int pageSize = 100);
    Task<int> GetTotalCountAsync(string search = "");
    Task<List<ProductListItem>> GetByCategoryAsync(int idCategoria);
    Task<ProductRepository.ProductDuplicateCheckResult> CheckDuplicatesAsync(ProductUpsertModel model);
    Task<bool> BarcodeExistsAsync(string codigoBarras, int excludeProductId = 0);
    Task<List<CategoryItem>> GetCategoriesAsync();
    Task SaveAsync(ProductUpsertModel model);
}
