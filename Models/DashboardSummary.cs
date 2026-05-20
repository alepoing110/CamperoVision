namespace CamperoDesktop.Models;

public class DashboardSummary
{
    public int TotalProductos { get; set; }
    public int TotalClientes { get; set; }
    public int TotalAlmacenes { get; set; }
    public int TotalNotasVenta { get; set; }
    public decimal VentasHoy { get; set; }
    public decimal VentasMes { get; set; }
    public int NotasEmitidasHoy { get; set; }
}
