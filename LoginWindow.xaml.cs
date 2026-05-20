using System.Windows;
using CamperoDesktop.ViewModels;

namespace CamperoDesktop;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
