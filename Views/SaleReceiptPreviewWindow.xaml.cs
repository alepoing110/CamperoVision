using System;
using System.IO;
using System.Windows;
using CamperoDesktop.Data;

namespace CamperoDesktop.Views;

public partial class SaleReceiptPreviewWindow : Window
{
    private readonly string _pdfPath;

    public SaleReceiptPreviewWindow(string pdfPath, string noteNumber)
    {
        InitializeComponent();
        _pdfPath = pdfPath;
        TxtTitle.Text = $"Vista previa de nota: {noteNumber}";
        TxtPath.Text = pdfPath;
        Loaded += SaleReceiptPreviewWindow_Loaded;
    }

    private void SaleReceiptPreviewWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TryLoadPreview();
    }

    private void TryLoadPreview()
    {
        try
        {
            if (!File.Exists(_pdfPath))
            {
                ShowFallback();
                return;
            }

            PdfBrowser.Navigate(new Uri(_pdfPath));
        }
        catch
        {
            ShowFallback();
        }
    }

    private void ShowFallback()
    {
        FallbackPanel.Visibility = Visibility.Visible;
    }

    private void AbrirPdf_Click(object sender, RoutedEventArgs e)
    {
        SaleReceiptService.OpenReceipt(_pdfPath);
    }

    private void Imprimir_Click(object sender, RoutedEventArgs e)
    {
        bool printed = SaleReceiptService.TryPrintReceipt(_pdfPath);
        if (!printed)
        {
            MessageBox.Show(
                "No se pudo enviar la nota directamente a impresion. Se abrira el PDF para imprimirlo manualmente.",
                "Impresion",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            SaleReceiptService.OpenReceipt(_pdfPath);
            return;
        }

        MessageBox.Show(
            "La nota fue enviada a impresion.",
            "Impresion",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
