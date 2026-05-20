using System.Windows.Controls;
using System.Windows.Input;
using CamperoDesktop.ViewModels;

namespace CamperoDesktop.Views;

public partial class SalesRegisterView : UserControl
{
    public SalesRegisterView()
    {
        InitializeComponent();
    }

    private void SalesHistoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SalesNotesViewModel viewModel && viewModel.EditHistorySaleCommand.CanExecute(null))
        {
            viewModel.EditHistorySaleCommand.Execute(null);
        }
    }
}
