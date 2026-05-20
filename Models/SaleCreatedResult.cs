namespace CamperoDesktop.Models;

public class SaleCreatedResult
{
    public int IdNota { get; set; }
    public string NroNota { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
}
