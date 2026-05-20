using System.Windows;
using System.Windows.Controls;

namespace CamperoDesktop.Services;

public class MessageBoxDialogService : IDialogService
{
    public void ShowInfo(string message, string title = "Informacion")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowWarning(string message, string title = "Advertencia")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowSuccess(string message, string title = "Exito")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool? ShowConfirmation(string message, string title = "Confirmar")
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public bool ShowConfirmation(string title, string message, string okText, string cancelText)
    {
        var dialog = new ConfirmationDialog(title, message, okText, cancelText);
        return dialog.ShowDialog() == true;
    }

    public string? ShowInputDialog(string title, string message, string defaultValue = "")
    {
        InputDialog dialog = new(title, message, defaultValue);
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }
}

public class InputDialog : Window
{
    private readonly TextBox _textBox;

    public InputDialog(string title, string message, string defaultValue)
    {
        Title = title;
        Width = 400;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

        var label = new System.Windows.Controls.Label { Content = message };
        stackPanel.Children.Add(label);

        _textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 10) };
        stackPanel.Children.Add(_textBox);

        var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        var cancelButton = new System.Windows.Controls.Button { Content = "Cancelar", Width = 75 };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        stackPanel.Children.Add(buttonPanel);

        Content = stackPanel;
        _textBox.Focus();
        _textBox.SelectAll();
    }

    public string InputText => _textBox.Text;
}

public class ConfirmationDialog : Window
{
    public ConfirmationDialog(string title, string message, string okText, string cancelText)
    {
        Title = title;
        Width = 450;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

        var label = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        stackPanel.Children.Add(label);

        var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var okButton = new System.Windows.Controls.Button { Content = okText, Width = 100, Margin = new Thickness(0, 0, 10, 0) };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        var cancelButton = new System.Windows.Controls.Button { Content = cancelText, Width = 100 };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        stackPanel.Children.Add(buttonPanel);

        Content = stackPanel;
    }
}
