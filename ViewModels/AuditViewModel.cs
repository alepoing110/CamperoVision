using System.Collections.ObjectModel;
using System.Text;
using CamperoDesktop.Commands;
using CamperoDesktop.Data;
using CamperoDesktop.Helpers;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class AuditViewModel : ViewModelBase
{
    private readonly AuditRepository _repository;
    private readonly IAuditService _auditService;
    private readonly IDialogService _dialogService;
    private string _buscar = string.Empty;
    private string _estado = string.Empty;
    private string? _filtroTabla;
    private string? _filtroAccion;
    private DateTime? _filtroDesde;
    private DateTime? _filtroHasta;
    private int _paginaActual = 1;
    private int _totalPaginas = 1;
    private int _totalRegistros;
    private int _pageSize = 50;
    private bool _isBusy;
    private AuditLog? _selectedLog;
    private string _detalleJson = string.Empty;

    public AuditViewModel(AuditRepository repository, IAuditService auditService, IDialogService dialogService)
    {
        _repository = repository;
        _auditService = auditService;
        _dialogService = dialogService;
        BuscarCommand = new AsyncRelayCommand(() => LoadAsync(1));
        LimpiarFiltrosCommand = new AsyncRelayCommand(LimpiarFiltrosAsync);
        LimpiarAntiguosCommand = new AsyncRelayCommand(LimpiarAntiguosAsync);
        PaginaAnteriorCommand = new AsyncRelayCommand(() => LoadAsync(_paginaActual - 1), () => _paginaActual > 1);
        PaginaSiguienteCommand = new AsyncRelayCommand(() => LoadAsync(_paginaActual + 1), () => _paginaActual < _totalPaginas);
        TablasFiltro = new ObservableCollection<string>();
        AccionesFiltro = new ObservableCollection<string>();
    }

    public ObservableCollection<AuditLog> Logs { get; } = new();
    public ObservableCollection<string> TablasFiltro { get; }
    public ObservableCollection<string> AccionesFiltro { get; }
    public AsyncRelayCommand BuscarCommand { get; }
    public AsyncRelayCommand LimpiarFiltrosCommand { get; }
    public AsyncRelayCommand LimpiarAntiguosCommand { get; }
    public AsyncRelayCommand PaginaAnteriorCommand { get; }
    public AsyncRelayCommand PaginaSiguienteCommand { get; }

    public string Buscar
    {
        get => _buscar;
        set => SetProperty(ref _buscar, value);
    }

    public string Estado
    {
        get => _estado;
        set => SetProperty(ref _estado, value);
    }

    public string? FiltroTabla
    {
        get => _filtroTabla;
        set
        {
            if (SetProperty(ref _filtroTabla, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync(1));
            }
        }
    }

    public string? FiltroAccion
    {
        get => _filtroAccion;
        set
        {
            if (SetProperty(ref _filtroAccion, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync(1));
            }
        }
    }

    public DateTime? FiltroDesde
    {
        get => _filtroDesde;
        set
        {
            if (SetProperty(ref _filtroDesde, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync(1));
            }
        }
    }

    public DateTime? FiltroHasta
    {
        get => _filtroHasta;
        set
        {
            if (SetProperty(ref _filtroHasta, value))
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync(1));
            }
        }
    }

    public int PaginaActual
    {
        get => _paginaActual;
        set
        {
            if (SetProperty(ref _paginaActual, value) && value > 0)
            {
                AsyncHelper.FireAndForgetOnUi(async () => await LoadAsync(value));
            }
        }
    }

    public int TotalPaginas
    {
        get => _totalPaginas;
        set => SetProperty(ref _totalPaginas, value);
    }

    public int TotalRegistros
    {
        get => _totalRegistros;
        set => SetProperty(ref _totalRegistros, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public AuditLog? SelectedLog
    {
        get => _selectedLog;
        set
        {
            if (SetProperty(ref _selectedLog, value))
            {
                UpdateDetalleJson();
            }
        }
    }

    public string DetalleJson
    {
        get => _detalleJson;
        set => SetProperty(ref _detalleJson, value);
    }

    public async Task LoadAsync(int page = 1)
    {
        IsBusy = true;
        try
        {
            var logs = await _repository.GetAsync(_buscar, _filtroTabla, _filtroAccion, _filtroDesde, _filtroHasta, page, _pageSize);
            ReplaceCollection(Logs, logs);

            _totalRegistros = await _repository.GetTotalCountAsync(_buscar, _filtroTabla, _filtroAccion, _filtroDesde, _filtroHasta);
            _totalPaginas = Math.Max(1, (int)Math.Ceiling((double)_totalRegistros / _pageSize));
            _paginaActual = page;

            OnPropertyChanged(nameof(PaginaActual));
            OnPropertyChanged(nameof(TotalPaginas));
            OnPropertyChanged(nameof(TotalRegistros));
            PaginaAnteriorCommand.RaiseCanExecuteChanged();
            PaginaSiguienteCommand.RaiseCanExecuteChanged();

            Estado = $"{_totalRegistros} registro(s) - Pagina {PaginaActual} de {TotalPaginas}";

            if (TablasFiltro.Count == 0)
            {
                var tablas = await _repository.GetDistinctTablesAsync();
                TablasFiltro.Clear();
                foreach (var t in tablas) TablasFiltro.Add(t);

                var acciones = await _repository.GetDistinctActionsAsync();
                AccionesFiltro.Clear();
                foreach (var a in acciones) AccionesFiltro.Add(a);
            }

            UpdateDetalleJson();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateDetalleJson()
    {
        if (_selectedLog == null)
        {
            DetalleJson = "Selecciona un registro para ver los detalles.";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ID: {_selectedLog.Id}");
        sb.AppendLine($"Fecha: {_selectedLog.Fecha:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Usuario: {_selectedLog.Usuario}");
        sb.AppendLine($"Tabla: {_selectedLog.Tabla}");
        sb.AppendLine($"Accion: {_selectedLog.Accion}");
        sb.AppendLine($"Registro ID: {_selectedLog.RegistroId?.ToString() ?? "N/A"}");
        sb.AppendLine($"Descripcion: {_selectedLog.Descripcion}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(_selectedLog.DatosAnteriores))
        {
            sb.AppendLine("--- Datos Anteriores ---");
            sb.AppendLine(FormatJson(_selectedLog.DatosAnteriores));
            sb.AppendLine();
        }
        if (!string.IsNullOrEmpty(_selectedLog.DatosNuevos))
        {
            sb.AppendLine("--- Datos Nuevos ---");
            sb.AppendLine(FormatJson(_selectedLog.DatosNuevos));
        }
        DetalleJson = sb.ToString();
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    private async Task LimpiarFiltrosAsync()
    {
        _buscar = string.Empty;
        _filtroTabla = null;
        _filtroAccion = null;
        _filtroDesde = null;
        _filtroHasta = null;
        OnPropertyChanged(nameof(Buscar));
        OnPropertyChanged(nameof(FiltroTabla));
        OnPropertyChanged(nameof(FiltroAccion));
        OnPropertyChanged(nameof(FiltroDesde));
        OnPropertyChanged(nameof(FiltroHasta));
        await LoadAsync(1);
    }

    private async Task LimpiarAntiguosAsync()
    {
        bool confirmed = _dialogService.ShowConfirmation(
            "Limpiar registros antiguos",
            "Se eliminaran los registros de auditoria de mas de 90 dias. Esta accion no se puede deshacer.",
            "Eliminar",
            "Cancelar");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var cutoff = DateTime.Now.AddDays(-90);
            int deleted = await _repository.DeleteOlderThanAsync(cutoff);
            _dialogService.ShowSuccess($"Se eliminaron {deleted} registros antiguos.");
            await LoadAsync(1);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
