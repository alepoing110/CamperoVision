namespace CamperoDesktop.Models;

public class SaleReceiptDocument
{
    public string NroNota { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string CiNit { get; set; } = string.Empty;
    public string CodigoCliente { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public decimal MontoRecibido { get; set; }
    public decimal Cambio { get; set; }
    public decimal MontoGiftCard { get; set; }
    public List<SaleReceiptItem> Items { get; set; } = new();
}

public class SaleReceiptItem
{
    public string Codigo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public decimal Subtotal { get; set; }
}
