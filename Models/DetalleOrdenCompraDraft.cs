namespace CamperoDesktop.Models;

public class DetalleOrdenCompraDraft
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = "";
    public string Producto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal => Cantidad * PrecioUnitario;
}
