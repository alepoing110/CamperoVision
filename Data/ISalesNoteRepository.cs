using CamperoDesktop.Models;

namespace CamperoDesktop.Data;

public interface ISalesNoteRepository
{
    Task<SaleNoteEditModel> GetEditModelAsync(int idNota);
    Task UpdateAsync(SaleNoteEditModel model);
    Task<List<SaleNoteListItem>> GetAllAsync(int page = 1, int pageSize = 100);
    Task<SaleCreatedResult> CreateSaleAsync(SaleCreateRequest request);
    Task AnularNotaAsync(int idNota, int idUsuario, string motivo);
}
