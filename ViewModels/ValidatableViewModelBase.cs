using System.ComponentModel;

namespace CamperoDesktop.ViewModels;

public abstract class ValidatableViewModelBase : ViewModelBase, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Count != 0;

    public System.Collections.IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return _errors.SelectMany(kvp => kvp.Value).ToList();
        }

        return _errors.TryGetValue(propertyName, out List<string>? errors)
            ? errors
            : Array.Empty<string>();
    }

    protected void SetErrors(string propertyName, IEnumerable<string> errors)
    {
        List<string> normalizedErrors = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct()
            .ToList();

        if (normalizedErrors.Count == 0)
        {
            ClearErrors(propertyName);
            return;
        }

        _errors[propertyName] = normalizedErrors;
        OnErrorsChanged(propertyName);
    }

    protected void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            OnErrorsChanged(propertyName);
        }
    }

    protected void ClearAllErrors()
    {
        foreach (string propertyName in _errors.Keys.ToList())
        {
            _errors.Remove(propertyName);
            OnErrorsChanged(propertyName);
        }
    }

    private void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    protected bool ValidateRequired(string value, string propertyName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SetErrors(propertyName, new[] { $"{fieldName} es requerido." });
            return false;
        }
        ClearErrors(propertyName);
        return true;
    }

    protected bool ValidatePositiveDecimal(string value, string propertyName, string fieldName)
    {
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result) || result < 0)
        {
            SetErrors(propertyName, new[] { $"{fieldName} debe ser un valor positivo valido." });
            return false;
        }
        ClearErrors(propertyName);
        return true;
    }

    protected bool ValidatePositiveInteger(string value, string propertyName, string fieldName)
    {
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            SetErrors(propertyName, new[] { $"{fieldName} debe ser un numero mayor a cero." });
            return false;
        }
        ClearErrors(propertyName);
        return true;
    }
}
