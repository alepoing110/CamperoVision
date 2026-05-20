namespace CamperoDesktop.Services;

public interface IDialogService
{
    void ShowInfo(string message, string title = "Informacion");
    void ShowError(string message, string title = "Error");
    void ShowWarning(string message, string title = "Advertencia");
    void ShowSuccess(string message, string title = "Exito");
    bool? ShowConfirmation(string message, string title = "Confirmar");
    bool ShowConfirmation(string title, string message, string okText, string cancelText);
    string? ShowInputDialog(string title, string message, string defaultValue = "");
}
