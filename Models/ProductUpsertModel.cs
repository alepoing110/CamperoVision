namespace CamperoDesktop.Models;

public class ProductUpsertModel
{
    public int IdProducto { get; set; }
    public int IdCategoria { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal PrecioCompra { get; set; }
    public decimal PrecioVenta { get; set; }
    public string UnidadMedida { get; set; } = "unidad";
    public int StockMinimo { get; set; }
    public bool Activo { get; set; } = true;
}
