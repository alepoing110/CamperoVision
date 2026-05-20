using CamperoDesktop.Commands;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class BusinessSettingsViewModel : ValidatableViewModelBase
{
    private readonly BusinessSettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private string _name = string.Empty;
    private string _address = string.Empty;
    private string _phone = string.Empty;
    private string _nit = string.Empty;
    private string _email = string.Empty;
    private string _branch = string.Empty;
    private string _footerMessage = string.Empty;
    private string _logoPath = string.Empty;
    private string _statusText = "Carga los datos del establecimiento y guarda.";

    public BusinessSettingsViewModel(BusinessSettingsService settingsService, IDialogService dialogService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        SaveCommand = new RelayCommand(Save);
        ReloadCommand = new RelayCommand(Load);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ReloadCommand { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                ValidateName();
            }
        }
    }

    public string Address
    {
        get => _address;
        set
        {
            if (SetProperty(ref _address, value))
            {
                ValidateAddress();
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Nit
    {
        get => _nit;
        set => SetProperty(ref _nit, value);
    }

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
            {
                ValidateEmail();
            }
        }
    }

    public string Branch
    {
        get => _branch;
        set => SetProperty(ref _branch, value);
    }

    public string FooterMessage
    {
        get => _footerMessage;
        set => SetProperty(ref _footerMessage, value);
    }

    public string LogoPath
    {
        get => _logoPath;
        set => SetProperty(ref _logoPath, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public Task InitializeAsync()
    {
        Load();
        return Task.CompletedTask;
    }

    private void Load()
    {
        try
        {
            ClearAllErrors();
            BusinessSettingsModel model = _settingsService.Load();
            Name = model.Name;
            Address = model.Address;
            Phone = model.Phone;
            Nit = model.Nit;
            Email = model.Email;
            Branch = model.Branch;
            FooterMessage = model.FooterMessage;
            LogoPath = model.LogoPath;
            StatusText = "Datos cargados desde appsettings.json.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo cargar la configuracion del negocio.\n\nDetalle: {ex.Message}");
        }
    }

    private void Save()
    {
        ValidateName();
        ValidateAddress();
        ValidateEmail();
        if (HasErrors)
        {
            _dialogService.ShowWarning("Corrige los campos marcados antes de guardar.");
            return;
        }

        try
        {
            _settingsService.Save(new BusinessSettingsModel
            {
                Name = Name,
                Address = Address,
                Phone = Phone,
                Nit = Nit,
                Email = Email,
                Branch = Branch,
                FooterMessage = FooterMessage,
                LogoPath = LogoPath
            });

            StatusText = "Configuracion guardada. Los nuevos datos ya pueden usarse en impresos y reportes.";
            _dialogService.ShowInfo("Configuracion del negocio guardada correctamente.", "Negocio");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"No se pudo guardar la configuracion del negocio.\n\nDetalle: {ex.Message}");
        }
    }

    private void ValidateName()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            SetErrors(nameof(Name), new[] { "Ingresa el nombre del negocio." });
            return;
        }

        ClearErrors(nameof(Name));
    }

    private void ValidateAddress()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            SetErrors(nameof(Address), new[] { "Ingresa la direccion del establecimiento." });
            return;
        }

        ClearErrors(nameof(Address));
    }

    private void ValidateEmail()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ClearErrors(nameof(Email));
            return;
        }

        if (!Email.Contains('@') || Email.StartsWith('@') || Email.EndsWith('@'))
        {
            SetErrors(nameof(Email), new[] { "Ingresa un email valido o deja el campo vacio." });
            return;
        }

        ClearErrors(nameof(Email));
    }
}
