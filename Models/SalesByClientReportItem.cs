namespace CamperoDesktop.Models;

public class SalesByClientReportItem
{
    public string Cliente { get; set; } = string.Empty;
    public string CiNit { get; set; } = string.Empty;
    public int CantidadNotas { get; set; }
    public decimal TotalVendido { get; set; }
}

