using System.Windows;
using CamperoDesktop.ViewModels;

namespace CamperoDesktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
