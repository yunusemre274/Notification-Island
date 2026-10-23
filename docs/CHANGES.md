# Değişiklik dökümü

Bu döküm önceki Liquid Glass değişiklikleri ile yerel ajan/alarm geliştirmelerinin mevcut çalışma ağacını kapsar. Git geçmişi değiştirilmedi.

Yeni teslimat: [ROADMAP_UPDATE.md](../ROADMAP_UPDATE.md). İşlev ve kaynak kanıtları: [BENCHMARKS.md](../BENCHMARKS.md).

| Dosya | Durum |
|---|---|
| [.gitignore](../.gitignore) | Eklendi |
| [AGENT_ARCHITECTURE.md](../AGENT_ARCHITECTURE.md) | Eklendi |
| [ARCHITECTURE.md](../ARCHITECTURE.md) | Güncellendi |
| [Agent/AgentRuntime.cs](../Agent/AgentRuntime.cs) | Eklendi |
| [Agent/Alarms/AlarmModels.cs](../Agent/Alarms/AlarmModels.cs) | Eklendi |
| [Agent/Alarms/AlarmService.cs](../Agent/Alarms/AlarmService.cs) | Eklendi |
| [Agent/Alarms/TurkishAlarmParser.cs](../Agent/Alarms/TurkishAlarmParser.cs) | Eklendi |
| [Agent/Contracts.cs](../Agent/Contracts.cs) | Eklendi |
| [Agent/DurableStore.cs](../Agent/DurableStore.cs) | Eklendi |
| [Agent/Files/FileOrganizer.cs](../Agent/Files/FileOrganizer.cs) | Eklendi |
| [Agent/Files/OperationJournal.cs](../Agent/Files/OperationJournal.cs) | Eklendi |
| [Agent/Files/PathGuard.cs](../Agent/Files/PathGuard.cs) | Eklendi |
| [Agent/Ollama/OllamaChatClient.cs](../Agent/Ollama/OllamaChatClient.cs) | Eklendi |
| [Agent/OperationReceipts.cs](../Agent/OperationReceipts.cs) | Eklendi |
| [Agent/PermissionPolicy.cs](../Agent/PermissionPolicy.cs) | Eklendi |
| [Agent/ToolRegistry.cs](../Agent/ToolRegistry.cs) | Eklendi |
| [Agent/Tools/DesktopTools.cs](../Agent/Tools/DesktopTools.cs) | Eklendi |
| [BENCHMARKS.md](../BENCHMARKS.md) | Eklendi |
| [Benchmarks/Collector/Collector.csproj](../Benchmarks/Collector/Collector.csproj) | Eklendi |
| [Benchmarks/Collector/Counters.cs](../Benchmarks/Collector/Counters.cs) | Eklendi |
| [Benchmarks/Collector/Native.cs](../Benchmarks/Collector/Native.cs) | Eklendi |
| [Benchmarks/Collector/Program.cs](../Benchmarks/Collector/Program.cs) | Eklendi |
| [Benchmarks/NativeHost/main.cpp](../Benchmarks/NativeHost/main.cpp) | Eklendi |
| [Benchmarks/WpfBaseline/Program.cs](../Benchmarks/WpfBaseline/Program.cs) | Eklendi |
| [Benchmarks/WpfBaseline/WpfBaseline.csproj](../Benchmarks/WpfBaseline/WpfBaseline.csproj) | Eklendi |
| [Benchmarks/results/agent-tests.txt](../Benchmarks/results/agent-tests.txt) | Eklendi |
| [Benchmarks/results/collector-cross-build.txt](../Benchmarks/results/collector-cross-build.txt) | Eklendi |
| [Benchmarks/results/core-tests.txt](../Benchmarks/results/core-tests.txt) | Eklendi |
| [Benchmarks/results/validation-macos.json](../Benchmarks/results/validation-macos.json) | Eklendi |
| [Benchmarks/results/wpfAndWindowsLayout-cross-build.txt](../Benchmarks/results/wpfAndWindowsLayout-cross-build.txt) | Eklendi |
| [Benchmarks/results/wpfBaseline-cross-build.txt](../Benchmarks/results/wpfBaseline-cross-build.txt) | Eklendi |
| [Configuration/AgentOptions.cs](../Configuration/AgentOptions.cs) | Eklendi |
| [Configuration/AppearanceSettings.cs](../Configuration/AppearanceSettings.cs) | Eklendi |
| [Controls/Converters.cs](../Controls/Converters.cs) | Eklendi |
| [Controls/IslandIcon.cs](../Controls/IslandIcon.cs) | Eklendi |
| [Controls/MetricCard.cs](../Controls/MetricCard.cs) | Eklendi |
| [Core/ObservableObject.cs](../Core/ObservableObject.cs) | Eklendi |
| [Core/RelayCommand.cs](../Core/RelayCommand.cs) | Eklendi |
| `Core/TopBarStateController.cs` | Kaldırıldı (önceki yeniden tasarım) |
| [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md) | Güncellendi |
| [MainWindow.xaml](../MainWindow.xaml) | Güncellendi |
| [MainWindow.xaml.cs](../MainWindow.xaml.cs) | Güncellendi |
| [Models/AppSettings.cs](../Models/AppSettings.cs) | Güncellendi |
| [Models/NotificationItem.cs](../Models/NotificationItem.cs) | Eklendi |
| [NI.csproj](../NI.csproj) | Güncellendi |
| [PERFORMANCE_BUDGET.md](../PERFORMANCE_BUDGET.md) | Eklendi |
| [Packaging/Assets/Square150x150Logo.png](../Packaging/Assets/Square150x150Logo.png) | Eklendi |
| [Packaging/Assets/Square44x44Logo.png](../Packaging/Assets/Square44x44Logo.png) | Eklendi |
| [Packaging/Assets/StoreLogo.png](../Packaging/Assets/StoreLogo.png) | Eklendi |
| [Packaging/Package.appxmanifest](../Packaging/Package.appxmanifest) | Eklendi |
| [Program.cs](../Program.cs) | Güncellendi |
| [README.md](../README.md) | Güncellendi |
| [ROADMAP_UPDATE.md](../ROADMAP_UPDATE.md) | Eklendi |
| [Services/AI/OllamaClient.cs](../Services/AI/OllamaClient.cs) | Güncellendi |
| [Services/Agent/LocalAgentCoordinator.cs](../Services/Agent/LocalAgentCoordinator.cs) | Eklendi |
| [Services/Agent/WindowsUserFolders.cs](../Services/Agent/WindowsUserFolders.cs) | Eklendi |
| [Services/Alarms/AlarmHost.cs](../Services/Alarms/AlarmHost.cs) | Eklendi |
| [Services/Alarms/WindowsAlarmScheduler.cs](../Services/Alarms/WindowsAlarmScheduler.cs) | Eklendi |
| [Services/AudioEndpointService.cs](../Services/AudioEndpointService.cs) | Eklendi |
| [Services/BrightnessService.cs](../Services/BrightnessService.cs) | Güncellendi |
| `Services/DesktopVisibilityService.cs` | Kaldırıldı (önceki yeniden tasarım) |
| [Services/Media/IMediaService.cs](../Services/Media/IMediaService.cs) | Eklendi |
| [Services/MediaSessionService.cs](../Services/MediaSessionService.cs) | Güncellendi |
| `Services/NotificationService.cs` | Kaldırıldı (önceki yeniden tasarım) |
| [Services/Notifications/INotificationProvider.cs](../Services/Notifications/INotificationProvider.cs) | Eklendi |
| [Services/Notifications/NotificationActionHandler.cs](../Services/Notifications/NotificationActionHandler.cs) | Eklendi |
| [Services/Notifications/NotificationRouter.cs](../Services/Notifications/NotificationRouter.cs) | Eklendi |
| [Services/Notifications/WindowsNotificationProvider.cs](../Services/Notifications/WindowsNotificationProvider.cs) | Eklendi |
| [Services/RadioService.cs](../Services/RadioService.cs) | Güncellendi |
| [Services/SystemMonitorService.cs](../Services/SystemMonitorService.cs) | Güncellendi |
| [Services/SystemMonitoring/ISystemMonitor.cs](../Services/SystemMonitoring/ISystemMonitor.cs) | Eklendi |
| [Services/WeatherService.cs](../Services/WeatherService.cs) | Eklendi |
| [Services/Windowing/WindowMaterialService.cs](../Services/Windowing/WindowMaterialService.cs) | Eklendi |
| [Styles/Controls.xaml](../Styles/Controls.xaml) | Eklendi |
| [Tests/Agent/NI.Agent.Tests.csproj](../Tests/Agent/NI.Agent.Tests.csproj) | Eklendi |
| [Tests/Agent/Program.cs](../Tests/Agent/Program.cs) | Eklendi |
| [Tests/CoreTests.cs](../Tests/CoreTests.cs) | Eklendi |
| [Tests/NI.Core.Tests.csproj](../Tests/NI.Core.Tests.csproj) | Eklendi |
| [Tests/Windows/LayoutChecks.cs](../Tests/Windows/LayoutChecks.cs) | Eklendi |
| [Tests/Windows/NI.LayoutChecks.csproj](../Tests/Windows/NI.LayoutChecks.csproj) | Eklendi |
| [Themes/ThemeManager.cs](../Themes/ThemeManager.cs) | Eklendi |
| [Themes/Tokens.xaml](../Themes/Tokens.xaml) | Eklendi |
| [ViewModels/IslandVM.Agent.cs](../ViewModels/IslandVM.Agent.cs) | Eklendi |
| [ViewModels/IslandVM.cs](../ViewModels/IslandVM.cs) | Güncellendi |
| [ViewModels/QuickControlsVM.cs](../ViewModels/QuickControlsVM.cs) | Eklendi |
| [Views/Controls/AssistantPanel.xaml](../Views/Controls/AssistantPanel.xaml) | Eklendi |
| [Views/Controls/AssistantPanel.xaml.cs](../Views/Controls/AssistantPanel.xaml.cs) | Eklendi |
| [Views/Controls/ConnectionsPanel.xaml](../Views/Controls/ConnectionsPanel.xaml) | Eklendi |
| [Views/Controls/ConnectionsPanel.xaml.cs](../Views/Controls/ConnectionsPanel.xaml.cs) | Eklendi |
| `Views/Controls/ControlCenterPanel.xaml` | Kaldırıldı (önceki yeniden tasarım) |
| `Views/Controls/ControlCenterPanel.xaml.cs` | Kaldırıldı (önceki yeniden tasarım) |
| [Views/Controls/DashboardPanel.xaml](../Views/Controls/DashboardPanel.xaml) | Eklendi |
| [Views/Controls/DashboardPanel.xaml.cs](../Views/Controls/DashboardPanel.xaml.cs) | Eklendi |
| [Views/Controls/MediaPanel.xaml](../Views/Controls/MediaPanel.xaml) | Eklendi |
| [Views/Controls/MediaPanel.xaml.cs](../Views/Controls/MediaPanel.xaml.cs) | Eklendi |
| [Views/Controls/NotificationsPanel.xaml](../Views/Controls/NotificationsPanel.xaml) | Eklendi |
| [Views/Controls/NotificationsPanel.xaml.cs](../Views/Controls/NotificationsPanel.xaml.cs) | Eklendi |
| [Views/Controls/SettingsPanel.xaml](../Views/Controls/SettingsPanel.xaml) | Eklendi |
| [Views/Controls/SettingsPanel.xaml.cs](../Views/Controls/SettingsPanel.xaml.cs) | Eklendi |
| `Views/Controls/SoundPanel.xaml` | Kaldırıldı (önceki yeniden tasarım) |
| `Views/Controls/SoundPanel.xaml.cs` | Kaldırıldı (önceki yeniden tasarım) |
| [Views/Controls/SystemStatusPanel.xaml](../Views/Controls/SystemStatusPanel.xaml) | Eklendi |
| [Views/Controls/SystemStatusPanel.xaml.cs](../Views/Controls/SystemStatusPanel.xaml.cs) | Eklendi |
| `Views/Controls/WifiPanel.xaml` | Kaldırıldı (önceki yeniden tasarım) |
| `Views/Controls/WifiPanel.xaml.cs` | Kaldırıldı (önceki yeniden tasarım) |
| [Views/IslandView.xaml](../Views/IslandView.xaml) | Güncellendi |
| [Views/IslandView.xaml.cs](../Views/IslandView.xaml.cs) | Güncellendi |
| [docs/CHANGES.md](../docs/CHANGES.md) | Eklendi |
| [docs/NOTIFICATIONS.md](../docs/NOTIFICATIONS.md) | Eklendi |
| [docs/WINDOWS-VALIDATION.md](../docs/WINDOWS-VALIDATION.md) | Eklendi |
| [scripts/Build-Msix.ps1](../scripts/Build-Msix.ps1) | Eklendi |
| [scripts/Measure-Resources.ps1](../scripts/Measure-Resources.ps1) | Eklendi |
