namespace CamperoDesktop.Models;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public class SaleNoteEditDetailModel : INotifyPropertyChanged
{
    private int _idProducto;
    private int? _idKit;
    private bool _isKit;
    private string _codigo = string.Empty;
    private string _producto = string.Empty;
    private string _descripcion = string.Empty;
    private string _unidadMedida = string.Empty;
    private int _cantidad;
    private decimal _precioUnitario;
    private decimal _descuentoMonto;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int IdProducto { get => _idProducto; set => SetField(ref _idProducto, value); }
    public int? IdKit { get => _idKit; set => SetField(ref _idKit, value); }
    public bool IsKit { get => _isKit; set => SetField(ref _isKit, value); }
    public string Codigo { get => _codigo; set => SetField(ref _codigo, value); }
    public string Producto { get => _producto; set => SetField(ref _producto, value); }
    public string Descripcion { get => _descripcion; set => SetField(ref _descripcion, value); }
    public string UnidadMedida { get => _unidadMedida; set => SetField(ref _unidadMedida, value); }
    public int Cantidad { get => _cantidad; set => SetField(ref _cantidad, value, true); }
    public decimal PrecioUnitario { get => _precioUnitario; set => SetField(ref _precioUnitario, value, true); }
    public decimal DescuentoMonto { get => _descuentoMonto; set => SetField(ref _descuentoMonto, value, true); }

    public decimal BaseSubtotal => Cantidad * PrecioUnitario;
    public decimal DescuentoAplicado => DescuentoMonto < 0 ? 0 : Math.Min(DescuentoMonto, BaseSubtotal);
    public decimal Subtotal => BaseSubtotal - DescuentoAplicado;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DescuentoAplicado)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtotal)));
        }
    }
}
