using System.Windows;
using CamperoDesktop.ViewModels;

namespace CamperoDesktop.Views;

public partial class SaleNoteEditWindow : Window
{
    public SaleNoteEditWindow(SaleNoteEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
