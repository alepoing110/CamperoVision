namespace CamperoDesktop.Models;

public class SaleNoteListItem
{
    public int IdNota { get; set; }
    public string NroNota { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Almacen { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
