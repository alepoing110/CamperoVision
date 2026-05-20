using System.Collections.ObjectModel;
using System.IO;
using CamperoDesktop.Commands;
using CamperoDesktop.Services;
using Microsoft.Extensions.Logging;

namespace CamperoDesktop.ViewModels;

public class BackupViewModel : ViewModelBase
{
    private readonly DatabaseBackupService _backupService;
    private readonly ILogger<BackupViewModel> _logger;
    private readonly IDialogService _dialogService;
    private string _status = "Listo para realizar backup o restauracion.";
    private bool _isBusy;

    public BackupViewModel(DatabaseBackupService backupService, ILogger<BackupViewModel> logger, IDialogService dialogService)
    {
        _backupService = backupService;
        _logger = logger;
        _dialogService = dialogService;
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync, () => !IsBusy);
        RefreshBackupsCommand = new RelayCommand(LoadBackups);
        LoadBackups();
    }

    public ObservableCollection<string> Backups { get; } = new();
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    public AsyncRelayCommand CreateBackupCommand { get; }
    public AsyncRelayCommand RestoreBackupCommand { get; }
    public RelayCommand RefreshBackupsCommand { get; }

    private string? SelectedBackup => Backups.Count > 0 ? Backups[0] : null;

    private void LoadBackups()
    {
        Backups.Clear();
        var backups = _backupService.GetBackups();
        foreach (var backup in backups)
        {
            var fileInfo = new FileInfo(backup);
            Backups.Add($"{fileInfo.Name} ({FormatSize(fileInfo.Length)}) - {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm}");
        }
        Status = Backups.Count > 0 ? $"{Backups.Count} backup(s) encontrado(s)." : "No se encontraron backups.";
    }

    private async Task CreateBackupAsync()
    {
        try
        {
            IsBusy = true;
            Status = "Creando backup...";
            _logger.LogInformation("Usuario solicitando backup de base de datos");

            string filePath = await _backupService.CreateBackupAsync();
            LoadBackups();
            Status = $"Backup creado exitosamente: {Path.GetFileName(filePath)}";
            _dialogService.ShowSuccess("Backup creado exitosamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear backup");
            Status = $"Error al crear backup: {ex.Message}";
            _dialogService.ShowError($"Error al crear backup: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreBackupAsync()
    {
        var backups = _backupService.GetBackups();
        if (backups.Count == 0)
        {
            _dialogService.ShowWarning("No hay backups disponibles para restaurar.");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Restaurar Backup", "¿Estas seguro de que deseas restaurar el backup mas reciente? Esta accion sobrescribira los datos actuales.", "Restaurar", "Cancelar");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            Status = "Restaurando backup...";
            _logger.LogInformation("Usuario solicitando restauracion de backup: {Backup}", backups[0]);

            await _backupService.RestoreBackupAsync(backups[0]);
            Status = "Restauracion completada exitosamente. Reinicia la aplicacion para ver los cambios.";
            _dialogService.ShowSuccess("Restauracion completada. Reinicia la aplicacion.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al restaurar backup");
            Status = $"Error al restaurar backup: {ex.Message}";
            _dialogService.ShowError($"Error al restaurar backup: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
