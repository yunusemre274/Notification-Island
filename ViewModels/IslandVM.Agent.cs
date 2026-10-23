using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;
using NI.Agent;
using NI.Agent.Alarms;
using NI.Core;
using NI.Services;
using NI.Services.Agent;
using NI.Services.Alarms;

namespace NI.ViewModels;

public sealed partial class IslandVM
{
    private LocalAgentCoordinator? _agent;
    private AlarmHost? _alarmHost;
    private DispatcherTimer? _taskSummaryTimer;
    private TaskSnapshot? _taskSnapshot;
    private string _taskSummary = "", _approvalText = "", _approvalHash = "", _alarmInput = "";
    private bool _showTaskSummary;
    private readonly Queue<AlarmDelivery> _alarmAlerts = new();
    public string TaskSummary => _taskSummary;
    public bool HasTaskSummary => IsAssistantBusy || _showTaskSummary;
    public string TaskDetails => _taskSnapshot == null ? "Henüz görev çalıştırılmadı." : $"{_taskSnapshot.State} • {_taskSnapshot.CompletedSteps} doğrulanmış araç sonucu • {_taskSnapshot.VerifiedChanges} değişiklik";
    public string ApprovalText { get => _approvalText; private set { Set(ref _approvalText, value); Raise(nameof(HasApproval)); } }
    public bool HasApproval => !string.IsNullOrEmpty(ApprovalText);
    public ObservableCollection<AlarmRecord> Alarms { get; } = new();
    public string AlarmInput { get => _alarmInput; set => Set(ref _alarmInput, value); }
    public bool HasAlarmAlert => _alarmAlerts.Count > 0;
    public string AlarmAlert => _alarmAlerts.TryPeek(out var alert) ? (alert.Missed ? "Kaçırılmış alarm: " : "Alarm: ") + alert.Title : "";
    private string _operationDetails = "";
    public string OperationDetails { get => _operationDetails; private set => Set(ref _operationDetails, value); }
    public int AgentMaxTurns { get => _settings.Agent.MaxTurns; set { _settings.Agent.MaxTurns = Math.Clamp(value, 1, 24); Raise(); QueueSave(); } }
    public int AgentTaskSeconds { get => _settings.Agent.MaxTaskSeconds; set { _settings.Agent.MaxTaskSeconds = Math.Clamp(value, 30, 1800); Raise(); QueueSave(); } }
    public string AlarmTimeZone
    {
        get => _settings.Agent.TimeZoneId;
        set { try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); _settings.Agent.TimeZoneId = value; QueueSave(); } catch { StatusMessage = "Geçerli bir sistem saat dilimi kimliği girin."; } Raise(); }
    }
    public ICommand OperationDetailsCommand { get; private set; } = null!;
    public ICommand CancelTaskCommand { get; private set; } = null!;
    public ICommand ApproveTaskCommand { get; private set; } = null!;
    public ICommand RejectTaskCommand { get; private set; } = null!;
    public ICommand UndoTaskCommand { get; private set; } = null!;
    public ICommand CreateAlarmCommand { get; private set; } = null!;
    public ICommand CancelAlarmCommand { get; private set; } = null!;
    public ICommand DismissAlarmCommand { get; private set; } = null!;
    public ICommand SnoozeAlarmCommand { get; private set; } = null!;
    private void InitializeAgentCommands()
    {
        OperationDetailsCommand = new AsyncCommand(async _ => { if (_agent != null) OperationDetails = await _agent.ReadDetailsAsync(_lifetime.Token); }, ReportError);
        CancelTaskCommand = new RelayCommand(_ => _agent?.Cancel());
        ApproveTaskCommand = new RelayCommand(_ => { _agent?.Approve(_approvalHash, true); ApprovalText = ""; });
        RejectTaskCommand = new RelayCommand(_ => { _agent?.Approve(_approvalHash, false); ApprovalText = ""; });
        UndoTaskCommand = new AsyncCommand(async _ =>
        {
            if (_agent == null) { StatusMessage = "Uygulama başlatılıyor."; return; }
            var result = await _agent.UndoAsync(_lifetime.Token); ShowToolResult(result);
        }, ReportError);
        CreateAlarmCommand = new AsyncCommand(async _ =>
        {
            if (_alarmHost == null) { StatusMessage = "Alarm servisi başlatılıyor."; return; }
            var request = AlarmInput;
            var result = await Task.Run(() => _alarmHost.Service.Create(request, "Hatırlatıcı", Guid.NewGuid().ToString("N"), _settings.Agent.TimeZoneId), _lifetime.Token);
            ShowToolResult(result); await RefreshAlarmsAsync();
        }, ReportError);
        CancelAlarmCommand = new AsyncCommand(async value =>
        {
            if (_alarmHost == null || value is not AlarmRecord alarm) return;
            var result = await Task.Run(() => _alarmHost.Service.Cancel($"Alarm {alarm.Id} iptal et", alarm.Id, Guid.NewGuid().ToString("N")), _lifetime.Token);
            ShowToolResult(result); await RefreshAlarmsAsync();
        }, ReportError);
        DismissAlarmCommand = new RelayCommand(_ => DismissAlarm());
        SnoozeAlarmCommand = new AsyncCommand(async _ =>
        {
            if (_alarmHost == null || !_alarmAlerts.TryPeek(out var alarm)) return;
            var result = await Task.Run(() => _alarmHost.Service.Update($"Alarm {alarm.Id} 10 dakika ertele", alarm.Id, true, Guid.NewGuid().ToString("N")), _lifetime.Token);
            ShowToolResult(result); if (result.Success) DismissAlarm(); await RefreshAlarmsAsync();
        }, ReportError);
    }
    private void InitializeAgentServices()
    {
        if (_alarmHost != null) return;
        _alarmHost = new AlarmHost();
        _alarmHost.Due += OnAlarmDue; _alarmHost.Start();
        _agent = new LocalAgentCoordinator(_settings, _alarmHost.Service, ExecuteSystemToolAsync);
        _agent.Changed += OnTaskChanged; _agent.ApprovalRequired += OnApprovalRequired;
        try { _agent.RecoverInterrupted(); } catch (Exception ex) { StatusMessage = "Önceki görev kaydı okunamadı: " + ex.GetType().Name; }
        _ = RefreshAlarmsAsync();
    }
    private async Task RunAgentAsync()
    {
        if (!_settings.EnableAiAssistant) { StatusMessage = "Yerel ajan ayarlarda kapalı; alarmlar kullanılabilir."; return; }
        if (string.IsNullOrWhiteSpace(AssistantInput)) return;
        if (_agent == null) { StatusMessage = "Ajan başlatılıyor."; return; }
        try { var result = await _agent.RunAsync(AssistantInput, _lifetime.Token); OnTaskChanged(_agent, result); }
        catch (AgentException ex) { AssistantResponse = ex.Message; StatusMessage = ex.Message; }
        finally { IsAssistantBusy = false; ApprovalText = ""; UpdateSummary(); await RefreshAlarmsAsync(); }
    }
    private void OnTaskChanged(object? sender, TaskSnapshot snapshot) => OnUi(() =>
    {
        _taskSnapshot = snapshot; _taskSummary = snapshot.Summary;
        IsAssistantBusy = snapshot.EndedAt == null;
        AssistantResponse = snapshot.Summary;
        Raise(nameof(TaskSummary)); Raise(nameof(TaskDetails)); Raise(nameof(HasTaskSummary));
        if (snapshot.EndedAt != null) ShowTransientTaskSummary(snapshot.Summary);
        UpdateSummary();
    });
    private void OnApprovalRequired(object? sender, ApprovalRequest request) => OnUi(() =>
    { _approvalHash = request.PlanHash; ApprovalText = request.Summary; IsAssistantOpen = true; });
    private void ShowToolResult(ToolResult result)
    {
        StatusMessage = result.UserMessage; AssistantResponse = result.UserMessage;
        ShowTransientTaskSummary(result.UserMessage);
    }
    private void ShowTransientTaskSummary(string summary)
    {
        _taskSummary = summary; _showTaskSummary = true;
        _taskSummaryTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
        _taskSummaryTimer.Tick -= OnTaskSummaryExpired; _taskSummaryTimer.Tick += OnTaskSummaryExpired;
        _taskSummaryTimer.Stop(); _taskSummaryTimer.Start();
        Raise(nameof(TaskSummary)); Raise(nameof(HasTaskSummary)); UpdateSummary();
    }
    private void OnTaskSummaryExpired(object? sender, EventArgs e)
    { _taskSummaryTimer?.Stop(); _showTaskSummary = false; Raise(nameof(HasTaskSummary)); UpdateSummary(); }
    private async Task RefreshAlarmsAsync()
    {
        if (_disposed || _alarmHost == null) return;
        try
        {
            var records = await Task.Run(() => _alarmHost.Service.List().Where(x => x.State != "Cancelled").Take(12).ToArray());
            OnUi(() => { Alarms.Clear(); foreach (var alarm in records) Alarms.Add(alarm); });
        }
        catch (Exception ex) { OnUi(() => StatusMessage = "Alarm deposu okunamadı: " + ex.GetType().Name); }
    }
    private void OnAlarmDue(object? sender, AlarmDelivery delivery) => OnUi(() =>
    {
        if (_alarmAlerts.Count < 256) _alarmAlerts.Enqueue(delivery);
        Raise(nameof(HasAlarmAlert)); Raise(nameof(AlarmAlert)); UpdateSummary();
        IsExpanded = true; AlarmHost.SignalSound(); _ = RefreshAlarmsAsync();
    });
    private void DismissAlarm()
    {
        if (_alarmAlerts.Count > 0) _alarmAlerts.Dequeue();
        Raise(nameof(HasAlarmAlert)); Raise(nameof(AlarmAlert)); UpdateSummary();
    }
    private async Task<ToolResult> ExecuteSystemToolAsync(string name, JsonElement args, ToolContext context, CancellationToken token)
    {
        if (name == "system_metrics")
        {
            var metrics = SystemMonitorService.ReadMemorySnapshot();
            return metrics.TotalGb.HasValue ? new(true, "", $"RAM: {metrics.UsedGb:0.00} / {metrics.TotalGb:0.00} GB (%{metrics.Ram:0}).", "", metrics)
                : ToolResult.Fail("METRICS_UNAVAILABLE", "Sistem ölçümü alınamadı.");
        }
        if (name == "audio_set_volume")
        {
            var percent = args.GetProperty("percent").GetInt32();
            var evidence = Regex.Match(context.UserRequest, @"(?:yüzde\s*|%\s*)(\d{1,3})|(\d{1,3})\s*%", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            var requested = evidence.Success ? int.Parse(evidence.Groups[1].Success ? evidence.Groups[1].Value : evidence.Groups[2].Value) : -1;
            if (percent is < 0 or > 100 || percent != requested) return ToolResult.Fail("VOLUME_MISMATCH", "Ses yüzdesi kullanıcı isteğinde açık ve araç parametresiyle aynı olmalı.");
            var verified = await _dispatcher.InvokeAsync(() => QuickControls.SetVolumeVerified(percent));
            return verified ? new(true, "", $"Ana ses %{percent} olarak doğrulandı.", context.TaskId.ToString("N"), VerifiedChanges: 1)
                : ToolResult.Fail("AUDIO_UNAVAILABLE", "Ses seviyesi doğrulanamadı.");
        }
        if (name == "media_pause")
        {
            var success = await _dispatcher.InvokeAsync(async () => await _media.TryPauseAsync());
            if (!await success) return ToolResult.Fail("MEDIA_UNSUPPORTED", "Medya duraklatma desteklenmiyor veya oturum yok.");
            for (var i = 0; i < 20; i++)
            {
                token.ThrowIfCancellationRequested();
                if (await _dispatcher.InvokeAsync(() => !_media.IsPlaying)) return new(true, "", "Medya duraklatıldığı doğrulandı.", context.TaskId.ToString("N"), VerifiedChanges: 1);
                await Task.Delay(100, token);
            }
            return ToolResult.Fail("MEDIA_NOT_VERIFIED", "Oynatıcı duraklatma isteğini aldı, fakat son durum doğrulanamadı.");
        }
        return ToolResult.Fail("UNKNOWN_TOOL", "Desteklenmeyen sistem aracı.");
    }
    private void DisposeAgentServices()
    {
        _taskSummaryTimer?.Stop();
        if (_agent != null) { _agent.Changed -= OnTaskChanged; _agent.ApprovalRequired -= OnApprovalRequired; _agent.Dispose(); }
        if (_alarmHost != null) { _alarmHost.Due -= OnAlarmDue; _alarmHost.Dispose(); }
    }
}
