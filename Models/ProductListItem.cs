namespace CamperoDesktop.Models;

public class ProductListItem
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal PrecioCompra { get; set; }
    public decimal PrecioVenta { get; set; }
    public int StockMinimo { get; set; }
    public bool Activo { get; set; }
}
