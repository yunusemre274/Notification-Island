using System.Windows;
using NI.Core;
using NI.Models;

namespace NI.ViewModels;

public sealed partial class IslandVM
{
    private IslandMode _mode;
    private int _notificationPage;
    public IslandMode Mode => _mode;
    public bool IsExpanded
    {
        get => _mode is IslandMode.Dashboard or IslandMode.Settings;
        set { if (value != IsExpanded) SetMode(value ? IslandMode.Dashboard : IslandMode.Compact); }
    }
    public bool IsSettingsOpen
    {
        get => _mode == IslandMode.Settings;
        set { if (value != IsSettingsOpen) SetMode(value ? IslandMode.Settings : IslandMode.Dashboard); }
    }
    public bool IsTaskInputOpen
    {
        get => _mode == IslandMode.TaskInput;
        set { if (value != IsTaskInputOpen) SetMode(value ? IslandMode.TaskInput : IslandMode.Compact); }
    }
    public bool IsDashboardVisible => _mode == IslandMode.Dashboard;
    public bool HasUtilitySections => Appearance.ShowConnections || Appearance.ShowSystemStatus || Appearance.ShowMedia;
    public double PanelWidth => IsExpanded ? Appearance.PanelWidth : Appearance.CompactWidth;
    public double CompactHeight => Appearance.CompactHeight;
    public double WindowCornerRadius => IsExpanded ? Appearance.CornerRadius : (CompactHeight + 2) / 2;
    public CornerRadius SurfaceCornerRadius => new(WindowCornerRadius);
    public double BodyMaxHeight => Math.Max(330, Appearance.ExpandedMaxHeight - CompactHeight - 2);

    private void SetMode(IslandMode value)
    {
        if (!Set(ref _mode, value, nameof(Mode))) return;
        foreach (var name in new[] { nameof(IsExpanded), nameof(IsSettingsOpen), nameof(IsTaskInputOpen), nameof(IsDashboardVisible),
                     nameof(PanelWidth), nameof(WindowCornerRadius), nameof(SurfaceCornerRadius) }) Raise(name);
        UpdateActivity();
    }

    public bool HasTaskCard => HasTaskSummary || _taskSnapshot != null || HasApproval || HasAlarmAlert;
    public double TaskCardHeight => HasApproval ? 154 : HasAlarmAlert ? 102 : 88;
    public string TaskCardTitle => HasAlarmAlert ? "Alarm" : HasApproval ? "Onay gerekiyor" : IsAssistantBusy ? "Görev çalışıyor" : "Son görev";
    public string TaskCardSummary => HasAlarmAlert ? AlarmAlert : HasApproval ? ApprovalText : TaskSummary;
    public int NotificationPageSize => HasTaskCard ? 1 : 3;
    public int NotificationPageCount => Math.Max(1, (Notifications.Items.Count + NotificationPageSize - 1) / NotificationPageSize);
    public string NotificationPageLabel => $"{_notificationPage + 1} / {NotificationPageCount}";
    public bool HasNotificationPages => NotificationPageCount > 1;
    public bool CanPreviousNotificationPage => _notificationPage > 0;
    public bool CanNextNotificationPage => _notificationPage + 1 < NotificationPageCount;
    public IReadOnlyList<NotificationItem> VisibleNotifications => Notifications.Items.Skip(_notificationPage * NotificationPageSize).Take(NotificationPageSize).ToArray();
    public RelayCommand PreviousNotificationPageCommand { get; private set; } = null!;
    public RelayCommand NextNotificationPageCommand { get; private set; } = null!;

    private void InitializePresentationCommands()
    {
        PreviousNotificationPageCommand = new(_ => { _notificationPage--; RefreshNotificationPage(); }, _ => CanPreviousNotificationPage);
        NextNotificationPageCommand = new(_ => { _notificationPage++; RefreshNotificationPage(); }, _ => CanNextNotificationPage);
    }
    private void RefreshNotificationPage()
    {
        _notificationPage = Math.Clamp(_notificationPage, 0, NotificationPageCount - 1);
        foreach (var name in new[] { nameof(VisibleNotifications), nameof(NotificationPageLabel), nameof(HasNotificationPages),
                     nameof(CanPreviousNotificationPage), nameof(CanNextNotificationPage) }) Raise(name);
        PreviousNotificationPageCommand?.Refresh(); NextNotificationPageCommand?.Refresh();
    }
    private void RefreshTaskPresentation()
    {
        foreach (var name in new[] { nameof(HasTaskCard), nameof(TaskCardHeight), nameof(TaskCardTitle), nameof(TaskCardSummary) }) Raise(name);
        RefreshNotificationPage();
    }
}
