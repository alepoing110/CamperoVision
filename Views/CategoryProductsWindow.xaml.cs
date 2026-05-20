using System.Windows;
using CamperoDesktop.Models;

namespace CamperoDesktop.Views;

public partial class CategoryProductsWindow : Window
{
    public CategoryProductsWindow(string categoryName, IReadOnlyCollection<ProductListItem> products)
    {
        InitializeComponent();
        TxtTitulo.Text = $"Productos de la categoría: {categoryName}";
        TxtResumen.Text = $"{products.Count} producto(s) encontrado(s).";
        ProductsGrid.ItemsSource = products;
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
