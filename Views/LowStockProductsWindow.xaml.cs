using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CamperoDesktop.Models;

namespace CamperoDesktop.Views;

public partial class LowStockProductsWindow : Window
{
    private readonly IReadOnlyCollection<LowStockReportItem> _products;

    public LowStockProductsWindow(IReadOnlyCollection<LowStockReportItem> products)
    {
        InitializeComponent();
        _products = products;
        TxtResumen.Text = products.Count == 0
            ? "No hay productos en stock mínimo."
            : $"{products.Count} producto(s) requieren atención.";
        ProductsGrid.ItemsSource = products;
    }

    private void Imprimir_Click(object sender, RoutedEventArgs e)
    {
        PrintDialog printDialog = new();
        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        FlowDocument document = BuildPrintableDocument();
        document.PageWidth = printDialog.PrintableAreaWidth;
        document.PageHeight = printDialog.PrintableAreaHeight;
        document.PagePadding = new Thickness(36);
        document.ColumnWidth = printDialog.PrintableAreaWidth;

        printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Productos en stock crítico");
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private FlowDocument BuildPrintableDocument()
    {
        FlowDocument document = new();

        Paragraph title = new(new Run("Productos en stock crítico"))
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        document.Blocks.Add(title);

        Paragraph summary = new(new Run(TxtResumen.Text))
        {
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 14)
        };
        document.Blocks.Add(summary);

        Table table = new();
        table.Columns.Add(new TableColumn { Width = new GridLength(170) });
        table.Columns.Add(new TableColumn { Width = new GridLength(110) });
        table.Columns.Add(new TableColumn { Width = new GridLength(280) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });

        TableRowGroup headerGroup = new();
        TableRow headerRow = new();
        headerRow.Cells.Add(BuildHeaderCell("Almacén"));
        headerRow.Cells.Add(BuildHeaderCell("Código"));
        headerRow.Cells.Add(BuildHeaderCell("Producto"));
        headerRow.Cells.Add(BuildHeaderCell("Cantidad"));
        headerRow.Cells.Add(BuildHeaderCell("Stock Min."));
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        TableRowGroup bodyGroup = new();
        foreach (LowStockReportItem product in _products)
        {
            TableRow row = new();
            row.Cells.Add(BuildBodyCell(product.Almacen));
            row.Cells.Add(BuildBodyCell(product.Codigo));
            row.Cells.Add(BuildBodyCell(product.Producto));
            row.Cells.Add(BuildBodyCell(product.Cantidad.ToString()));
            row.Cells.Add(BuildBodyCell(product.StockMinimo.ToString()));
            bodyGroup.Rows.Add(row);
        }

        if (_products.Count == 0)
        {
            TableRow emptyRow = new();
            TableCell emptyCell = new(new Paragraph(new Run("No hay productos en stock crítico.")))
            {
                ColumnSpan = 5,
                Padding = new Thickness(8),
                BorderThickness = new Thickness(0.5),
                BorderBrush = System.Windows.Media.Brushes.LightGray
            };
            emptyRow.Cells.Add(emptyCell);
            bodyGroup.Rows.Add(emptyRow);
        }

        table.RowGroups.Add(bodyGroup);
        document.Blocks.Add(table);

        return document;
    }

    private static TableCell BuildHeaderCell(string text)
    {
        return new TableCell(new Paragraph(new Run(text)))
        {
            Padding = new Thickness(6),
            FontWeight = FontWeights.Bold,
            Background = System.Windows.Media.Brushes.Gainsboro,
            BorderThickness = new Thickness(0.5),
            BorderBrush = System.Windows.Media.Brushes.Gray
        };
    }

    private static TableCell BuildBodyCell(string text)
    {
        return new TableCell(new Paragraph(new Run(text)))
        {
            Padding = new Thickness(6),
            BorderThickness = new Thickness(0.5),
            BorderBrush = System.Windows.Media.Brushes.LightGray
        };
    }
}
