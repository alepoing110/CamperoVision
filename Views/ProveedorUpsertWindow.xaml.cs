using System.Windows;
using CamperoDesktop.Data;
using CamperoDesktop.Models;

namespace CamperoDesktop.Views;

public partial class ProveedorUpsertWindow : Window
{
    private readonly ProveedorRepository _repository;
    private ProveedorUpsertModel Model { get; }

    public ProveedorUpsertWindow(ProveedorUpsertModel? model, ProveedorRepository repository)
    {
        InitializeComponent();
        Model = model ?? new ProveedorUpsertModel();
        LoadForm();
        _repository = repository;
        Owner = Application.Current.MainWindow;
    }

    private void LoadForm()
    {
        TxtNombre.Text = Model.Nombre;
        TxtNit.Text = Model.Nit;
        TxtTelefono.Text = Model.Telefono;
        TxtEmail.Text = Model.Email;
        TxtDireccion.Text = Model.Direccion;
        ChkActivo.IsChecked = Model.Activo;
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        Model.Nombre = TxtNombre.Text.Trim();
        Model.Nit = TxtNit.Text.Trim();
        Model.Telefono = TxtTelefono.Text.Trim();
        Model.Email = TxtEmail.Text.Trim();
        Model.Direccion = TxtDireccion.Text.Trim();
        Model.Activo = ChkActivo.IsChecked ?? false;

        if (string.IsNullOrWhiteSpace(Model.Nombre))
        {
            MessageBox.Show("Nombre es requerido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNombre.Focus();
            return;
        }

        try
        {
            if (Model.IdProveedor == 0)
            {
                Model.IdProveedor = await _repository.CreateAsync(Model);
            }
            else
            {
                await _repository.UpdateAsync(Model);
            }
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
