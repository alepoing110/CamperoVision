namespace CamperoDesktop.Models;

public class ProductOption
{
    public int IdProducto { get; set; }
    public int? IdKit { get; set; }
    public bool IsKit { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "unidad";
    public decimal PrecioVenta { get; set; }
    public int StockDisponible { get; set; }

    public string Display
    {
        get
        {
            string barcodePart = string.IsNullOrWhiteSpace(CodigoBarras) ? string.Empty : $" | CB: {CodigoBarras}";
            string prefix = IsKit ? "[KIT] " : string.Empty;
            return $"{prefix}{Codigo} - {Nombre}{barcodePart} (Stock: {StockDisponible})";
        }
    }
}
