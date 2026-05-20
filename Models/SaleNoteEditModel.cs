namespace CamperoDesktop.Models;

public class SaleNoteEditModel
{
    public int IdNota { get; set; }
    public int EditorUserId { get; set; }
    public string NroNota { get; set; } = string.Empty;
    public int? IdCliente { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerCiNit { get; set; } = string.Empty;
    public int IdAlmacen { get; set; }
    public decimal GeneralDiscount { get; set; }
    public string Estado { get; set; } = string.Empty;
    public List<SaleNoteEditDetailModel> Items { get; set; } = new();
}
