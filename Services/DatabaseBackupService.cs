using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CamperoDesktop.Services;

public class DatabaseBackupService
{
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly string _mysqldumpPath;
    private readonly string _mysqlPath;

    public DatabaseBackupService(ILogger<DatabaseBackupService> logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;

        var builder = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);
        _databaseName = builder.Database;

        _mysqldumpPath = FindExecutable("mysqldump");
        _mysqlPath = FindExecutable("mysql");
    }

    public async Task<string> CreateBackupAsync(string backupDirectory = "")
    {
        if (string.IsNullOrEmpty(backupDirectory))
        {
            backupDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        }

        if (!Directory.Exists(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"campero_backup_{timestamp}.sql";
        string filePath = Path.Combine(backupDirectory, fileName);

        _logger.LogInformation("Iniciando backup de base de datos: {Database}", _databaseName);

        var builder = new MySqlConnector.MySqlConnectionStringBuilder(_connectionString);

        var startInfo = new ProcessStartInfo
        {
            FileName = _mysqldumpPath,
            Arguments = $"--user={builder.UserID} --password={builder.Password} --host={builder.Server} --port={builder.Port} --databases {_databaseName} --single-transaction --quick --lock-tables=false --result-file=\"{filePath}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo iniciar mysqldump");
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Error en backup: {Error}", error);
            throw new InvalidOperationException($"Error al crear backup: {error}");
        }

        long fileSize = new FileInfo(filePath).Length;
        _logger.LogInformation("Backup completado: {FilePath} ({FileSize:N0} bytes)", filePath, fileSize);

        return filePath;
    }

    public async Task RestoreBackupAsync(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("El archivo de backup no existe", backupFilePath);
        }

        _logger.LogInformation("Iniciando restauracion de backup: {FilePath}", backupFilePath);

        var builder = new MySqlConnector.MySqlConnectionStringBuilder(_connectionString);

        var startInfo = new ProcessStartInfo
        {
            FileName = _mysqlPath,
            Arguments = $"--user={builder.UserID} --password={builder.Password} --host={builder.Server} --port={builder.Port} {_databaseName} < \"{backupFilePath}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo iniciar mysql");

        using var reader = new StreamReader(backupFilePath);
        await reader.BaseStream.CopyToAsync(process.StandardInput.BaseStream);
        process.StandardInput.Close();

        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Error en restauracion: {Error}", error);
            throw new InvalidOperationException($"Error al restaurar backup: {error}");
        }

        _logger.LogInformation("Restauracion completada exitosamente");
    }

    public List<string> GetBackups(string backupDirectory = "")
    {
        if (string.IsNullOrEmpty(backupDirectory))
        {
            backupDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        }

        if (!Directory.Exists(backupDirectory))
        {
            return new List<string>();
        }

        return Directory.GetFiles(backupDirectory, "campero_backup_*.sql")
            .OrderByDescending(f => f)
            .ToList();
    }

    private static string FindExecutable(string name)
    {
        string[] possiblePaths = new[]
        {
            name,
            Path.Combine("C:\\xampp\\mysql\\bin", $"{name}.exe"),
            Path.Combine("C:\\Program Files\\MySQL\\MySQL Server 8.0\\bin", $"{name}.exe"),
            Path.Combine("C:\\Program Files (x86)\\MySQL\\MySQL Server 8.0\\bin", $"{name}.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MySQL", "MySQL Server 8.0", "bin", $"{name}.exe"),
        };

        foreach (string path in possiblePaths)
        {
            try
            {
                if (File.Exists(path) || Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = false, CreateNoWindow = true }) != null)
                {
                    return path;
                }
            }
            catch
            {
                // Continue to next path
            }
        }

        return name;
    }
}
