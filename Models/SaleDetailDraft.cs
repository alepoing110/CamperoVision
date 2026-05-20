namespace CamperoDesktop.Models;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public class SaleDetailDraft : INotifyPropertyChanged
{
    private int _idProducto;
    private int? _idKit;
    private bool _isKit;
    private string _codigo = string.Empty;
    private string _codigoBarras = string.Empty;
    private string _producto = string.Empty;
    private string _descripcion = string.Empty;
    private string _unidadMedida = "unidad";
    private int _cantidad;
    private decimal _precioUnitario;
    private string _tipoDescuento = "Monto";
    private decimal _descuentoValor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int IdProducto { get => _idProducto; set => SetField(ref _idProducto, value); }
    public int? IdKit { get => _idKit; set => SetField(ref _idKit, value); }
    public bool IsKit { get => _isKit; set => SetField(ref _isKit, value); }
    public string Codigo { get => _codigo; set => SetField(ref _codigo, value); }
    public string CodigoBarras { get => _codigoBarras; set => SetField(ref _codigoBarras, value); }
    public string Producto { get => _producto; set => SetField(ref _producto, value); }
    public string Descripcion { get => _descripcion; set => SetField(ref _descripcion, value); }
    public string UnidadMedida { get => _unidadMedida; set => SetField(ref _unidadMedida, value); }
    public int Cantidad { get => _cantidad; set => SetField(ref _cantidad, value, true); }
    public decimal PrecioUnitario { get => _precioUnitario; set => SetField(ref _precioUnitario, value, true); }
    public string TipoDescuento { get => _tipoDescuento; set => SetField(ref _tipoDescuento, value, true); }
    public decimal DescuentoValor { get => _descuentoValor; set => SetField(ref _descuentoValor, value, true); }

    public decimal BaseSubtotal => Cantidad * PrecioUnitario;

    public decimal Descuento
    {
        get
        {
            decimal discount = TipoDescuento == "Porcentaje"
                ? BaseSubtotal * (DescuentoValor / 100m)
                : DescuentoValor;

            if (discount < 0)
            {
                return 0;
            }

            return discount > BaseSubtotal ? BaseSubtotal : discount;
        }
    }

    public decimal Subtotal => BaseSubtotal - Descuento;

    private void SetField<T>(ref T field, T value, bool updateDerived = false, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (updateDerived)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BaseSubtotal)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Descuento)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtotal)));
        }
    }
}
