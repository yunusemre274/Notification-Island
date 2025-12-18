using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NI.Services;
using NI.Services.AI;
using NI.ViewModels;

using NI.Views.Controls;

namespace NI.Views
{
    public enum PanelType { None, Wifi, Sound, ControlCenter }

    /// <summary>
    /// Music Island State - Controls hover and click expansion
    /// </summary>
    public enum MusicIslandState
    {
        Compact,         // Minimal pill
        HoverExpanded,   // Wider with album art and basic controls
        FullExpanded     // Full card with timeline and all controls
    }

    /// <summary>
    /// SINGLE SOURCE OF TRUTH for Island UI state
    /// </summary>
    public enum IslandState
    {
        Default,        // Compact pill - Active Window, idle, etc.
        MusicExpanded   // Square card - Spotify playback
    }

    public partial class IslandView : UserControl, INotifyPropertyChanged
    {
        private IslandVM _vm = null!;

        // SINGLE STATE PROPERTY - controls ALL visibility
        private IslandState _currentState = IslandState.Default;
        public IslandState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    OnPropertyChanged(nameof(CurrentState));
                    OnPropertyChanged(nameof(IsDefault));
                    OnPropertyChanged(nameof(IsMusicExpanded));

                    // Animate size when state changes
                    AnimateToCurrentState();
                }
            }
        }

        // Helper properties for XAML binding
        public bool IsDefault => CurrentState == IslandState.Default;
        public bool IsMusicExpanded => CurrentState == IslandState.MusicExpanded;

        // Music island state (for hover/click expansion)
        private MusicIslandState _musicState = MusicIslandState.Compact;
        public MusicIslandState MusicState
        {
            get => _musicState;
            private set
            {
                if (_musicState != value)
                {
                    _musicState = value;
                    OnPropertyChanged(nameof(MusicState));
                    OnPropertyChanged(nameof(IsCompact));
                    OnPropertyChanged(nameof(IsHoverExpanded));
                    OnPropertyChanged(nameof(IsFullExpanded));

                    // Animate size when music state changes
                    if (CurrentState == IslandState.MusicExpanded)
                    {
                        AnimateToCurrentState();
                    }
                }
            }
        }

        // Helper properties for music state
        public bool IsCompact => MusicState == MusicIslandState.Compact;
        public bool IsHoverExpanded => MusicState == MusicIslandState.HoverExpanded;
        public bool IsFullExpanded => MusicState == MusicIslandState.FullExpanded;

        // Panel state
        private PanelType _currentPanel = PanelType.None;
        private WifiPanel? _wifiPanel;
        private SoundPanel? _soundPanel;

        // REMOVED: MediaSessionService (now ONLY in IslandVM to prevent double initialization)

        // REMOVED: SystemMonitor and AI features (non-essential, causing complexity)
        // private SystemMonitorService? _systemMonitor;
        // private bool _isSystemInfoVisible;
        // private AiAssistantService? _aiAssistant;
        // private CancellationTokenSource? _aiCancellationTokenSource;

        // Event for MainWindow to handle Control Center
        public event EventHandler<PanelType>? PanelRequested;



        // Animation durations - FASTER for snappy HyperOS feel
        private static readonly Duration TransitionDuration = TimeSpan.FromMilliseconds(250);

        // Sizes
        private const double DefaultWidth = 350;
        private const double DefaultHeight = 36;
        private const double MusicCardWidth = 320;
        private const double MusicCardHeight = 90;

        public IslandView()
        {
            InitializeComponent();
            _vm = (IslandVM)DataContext;
            _vm.NotificationArrived += OnNotificationArrived;
            _vm.SmartEventArrived += OnSmartEventArrived;
            _vm.SpotifyChanged += OnSpotifyChanged;
            _vm.HeadphoneBannerArrived += OnHeadphoneBannerArrived;
            _vm.ActiveWindowChanged += OnActiveWindowChanged;

            // CRITICAL: Subscribe to IsSpotifyActive changes for automatic state transition
            _vm.PropertyChanged += OnViewModelPropertyChanged;

            _vm.Start();

            // REMOVED: MediaSessionService initialization (now ONLY in IslandVM)
            // REMOVED: SystemMonitor and AI initialization (non-essential features)
        }

        /// <summary>
        /// SINGLE PLACE for state transition logic
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_vm.IsSpotifyActive))
            {
                // AUTOMATIC STATE TRANSITION (ONLY PLACE)
                if (_vm.IsSpotifyActive)
                {
                    CurrentState = IslandState.MusicExpanded;
                    MusicState = MusicIslandState.Compact; // Start compact
                }
                else
                {
                    CurrentState = IslandState.Default;
                    MusicState = MusicIslandState.Compact; // Reset
                }
            }
        }

        // REMOVED: All non-essential features (System Monitor, AI Assistant) to reduce complexity and crashes


        // REMOVED: InitializeMediaSessionAsync - duplicate initialization caused crashes

        /// <summary>
        /// Called by MainWindow when visibility changes. Pauses/resumes VM timers.
        /// </summary>
        public void SetVisibility(bool visible)
        {
            _vm.SetVisibility(visible);
        }

        #region Hover Handling

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            // Hover expand when Spotify is active and not fully expanded
            if (CurrentState == IslandState.MusicExpanded && MusicState == MusicIslandState.Compact)
            {
                MusicState = MusicIslandState.HoverExpanded;
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            // Collapse to compact when leaving (unless fully expanded)
            if (CurrentState == IslandState.MusicExpanded && MusicState == MusicIslandState.HoverExpanded)
            {
                MusicState = MusicIslandState.Compact;
            }
        }

        #endregion

        #region Click Handling

        private void OnLeftClick(object sender, MouseButtonEventArgs e)
        {
            // Toggle full expansion when Spotify is active
            if (CurrentState == IslandState.MusicExpanded)
            {
                if (MusicState == MusicIslandState.FullExpanded)
                {
                    MusicState = MusicIslandState.Compact;
                }
                else
                {
                    MusicState = MusicIslandState.FullExpanded;
                }
                e.Handled = true;
            }
            else
            {
                PlayPulse();
            }
        }

        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            SettingsPopup.IsOpen = true;
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
            _vm.Stop();
            Application.Current.Shutdown();
        }

        private void OnCenterClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_vm.IsSpotifyPlaying)
            {
                SpotifyService.OpenSpotify();
            }
        }

        private void OnWifiClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            PanelRequested?.Invoke(this, PanelType.ControlCenter);
        }

        private void OnSoundClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            PanelRequested?.Invoke(this, PanelType.ControlCenter);
        }

        private void OnTogglePlayPause(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _vm.TogglePlayPause();
        }

        private void OnSkipPrevious(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _vm.SkipPrevious();
        }

        private void OnSkipNext(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _vm.SkipNext();
        }

        #endregion

        // REMOVED: Media Control Handlers - duplicate subscriptions caused crashes
        // REMOVED: System Info Card and AI Assistant features - non-essential complexity

        #region Panel Management

        private void TogglePanel(PanelType panelType)
        {
            if (panelType == PanelType.ControlCenter)
            {
                PanelRequested?.Invoke(this, panelType);
                return;
            }

            if (_currentPanel == panelType)
            {
                CloseCurrentPanel();
                return;
            }

            if (_currentPanel != PanelType.None)
            {
                CloseCurrentPanel(() => OpenPanel(panelType));
            }
            else
            {
                OpenPanel(panelType);
            }
        }

        private void OpenPanel(PanelType panelType)
        {
            _currentPanel = panelType;
            PanelContainer.Children.Clear();
            PanelContainer.Visibility = Visibility.Visible;

            switch (panelType)
            {
                case PanelType.Wifi:
                    _wifiPanel = new WifiPanel();
                    _wifiPanel.Opacity = 0;
                    PanelContainer.Children.Add(_wifiPanel);
                    _wifiPanel.AnimateIn();
                    break;

                case PanelType.Sound:
                    _soundPanel = new SoundPanel();
                    _soundPanel.Opacity = 0;
                    PanelContainer.Children.Add(_soundPanel);
                    _soundPanel.AnimateIn();
                    break;
            }

            // REMOVED: Expand() - panels handle their own sizing
        }

        private void CloseCurrentPanel(Action? onComplete = null)
        {
            var previousPanel = _currentPanel;
            _currentPanel = PanelType.None;

            Action cleanup = () =>
            {
                Dispatcher.Invoke(() =>
                {
                    PanelContainer.Children.Clear();
                    PanelContainer.Visibility = Visibility.Collapsed;
                    _wifiPanel = null;
                    _soundPanel = null;
                    onComplete?.Invoke();
                });
            };

            switch (previousPanel)
            {
                case PanelType.Wifi when _wifiPanel != null:
                    _wifiPanel.AnimateOut(cleanup);
                    break;
                case PanelType.Sound when _soundPanel != null:
                    _soundPanel.AnimateOut(cleanup);
                    break;
                default:
                    cleanup();
                    break;
            }
        }

        public void CloseAllPanels()
        {
            if (_currentPanel != PanelType.None)
            {
                CloseCurrentPanel();
            }
        }

        #endregion

        #region Notification Handling

        private void OnNotificationArrived(object? sender, EventArgs e)
        {
            // REMOVED: State managed automatically by IsSpotifyActive
        }

        private void OnSmartEventArrived(object? sender, EventArgs e)
        {
            // REMOVED: State managed automatically by IsSpotifyActive
        }

        private void OnSpotifyChanged(object? sender, EventArgs e)
        {
            // REMOVED: State managed automatically by IsSpotifyActive via PropertyChanged
        }

        private void OnHeadphoneBannerArrived(object? sender, EventArgs e)
        {
            // REMOVED: State managed automatically by IsSpotifyActive
        }

        private void OnActiveWindowChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateActiveWindowIcon();
                // REMOVED: UpdateExpandedContentVisibility - state is automatic
            });
        }

        private void UpdateActiveWindowIcon()
        {
            var iconKey = _vm.ActiveWindowIcon + "Icon";
            if (Resources.Contains(iconKey))
            {
                var icon = Resources[iconKey] as ImageSource;
                if (ActiveWindowIconCompact != null)
                    ActiveWindowIconCompact.Source = icon;
                // REMOVED: ActiveWindowIconExpanded - no longer exists in XAML
            }
            else
            {
                // Fallback to AppIcon
                var icon = Resources["AppIcon"] as ImageSource;
                if (ActiveWindowIconCompact != null)
                    ActiveWindowIconCompact.Source = icon;
                // REMOVED: ActiveWindowIconExpanded - no longer exists in XAML
            }
        }

        // REMOVED: UpdateExpandedContentVisibility - replaced by automatic state transitions

        #endregion

        #region Animations - Simple State Transitions

        /// <summary>
        /// Animates to the current state size (called automatically when CurrentState changes)
        /// </summary>
        private void AnimateToCurrentState()
        {
            double targetWidth, targetHeight, targetCornerRadius, targetScale;

            if (CurrentState == IslandState.MusicExpanded)
            {
                // Music state determines the size
                switch (MusicState)
                {
                    case MusicIslandState.FullExpanded:
                        targetWidth = 380;
                        targetHeight = 110;
                        targetCornerRadius = 24;
                        targetScale = 1.01;
                        break;

                    case MusicIslandState.HoverExpanded:
                        targetWidth = 350;
                        targetHeight = 86;
                        targetCornerRadius = 22;
                        targetScale = 1.005;
                        break;

                    case MusicIslandState.Compact:
                    default:
                        targetWidth = 300;
                        targetHeight = 74;
                        targetCornerRadius = 20;
                        targetScale = 1.0;
                        break;
                }
            }
            else
            {
                // Default state
                targetWidth = DefaultWidth;
                targetHeight = DefaultHeight;
                targetCornerRadius = 18;
                targetScale = 1.0;
            }

            // Smooth cubic easing for organic feel
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Animate size
            var widthAnim = new DoubleAnimation(targetWidth, TransitionDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(WidthProperty, widthAnim);

            var heightAnim = new DoubleAnimation(targetHeight, TransitionDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(HeightProperty, heightAnim);

            // Subtle scale for premium feel
            var scaleXAnim = new DoubleAnimation(targetScale, TransitionDuration) { EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation(targetScale, TransitionDuration) { EasingFunction = ease };
            IslandScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            IslandScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

            // Smooth corner radius transition
            var cornerAnim = new DoubleAnimation(targetCornerRadius, TransitionDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(Border.CornerRadiusProperty, null);
            IslandBorder.CornerRadius = new CornerRadius(targetCornerRadius);
        }

        private void PlayPulse()
        {
            var scaleUp = new DoubleAnimation(1.03, TimeSpan.FromMilliseconds(50));
            var scaleDown = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(50)) 
            { 
                BeginTime = TimeSpan.FromMilliseconds(50) 
            };

            var sb = new Storyboard();
            
            var scaleXUp = scaleUp.Clone();
            var scaleYUp = scaleUp.Clone();
            var scaleXDown = scaleDown.Clone();
            var scaleYDown = scaleDown.Clone();

            Storyboard.SetTarget(scaleXUp, IslandBorder);
            Storyboard.SetTarget(scaleYUp, IslandBorder);
            Storyboard.SetTarget(scaleXDown, IslandBorder);
            Storyboard.SetTarget(scaleYDown, IslandBorder);

            Storyboard.SetTargetProperty(scaleXUp, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            Storyboard.SetTargetProperty(scaleYUp, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
            Storyboard.SetTargetProperty(scaleXDown, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            Storyboard.SetTargetProperty(scaleYDown, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

            sb.Children.Add(scaleXUp);
            sb.Children.Add(scaleYUp);
            sb.Children.Add(scaleXDown);
            sb.Children.Add(scaleYDown);

            sb.Begin();
        }

        private void StartSpotifyBarAnimations()
        {
            if (_vm == null || !_vm.IsSpotifyActive || !_vm.IsSpotifyPlaying)
                return;

            var bar1 = FindName("SpotifyBar1") as FrameworkElement;
            var bar2 = FindName("SpotifyBar2") as FrameworkElement;
            var bar3 = FindName("SpotifyBar3") as FrameworkElement;

            // Animate bars with different phases
            if (bar1 != null) AnimateSpotifyBar(bar1, 8, 16, 0.4);
            if (bar2 != null) AnimateSpotifyBar(bar2, 10, 20, 0.6);
            if (bar3 != null) AnimateSpotifyBar(bar3, 6, 14, 0.5);
        }

        private void AnimateSpotifyBar(FrameworkElement bar, double minHeight, double maxHeight, double duration)
        {
            if (bar == null) return;

            var anim = new DoubleAnimation
            {
                From = minHeight,
                To = maxHeight,
                Duration = TimeSpan.FromSeconds(duration),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            bar.BeginAnimation(HeightProperty, anim);
        }

        private void StopSpotifyBarAnimations()
        {
            var bar1 = FindName("SpotifyBar1") as FrameworkElement;
            var bar2 = FindName("SpotifyBar2") as FrameworkElement;
            var bar3 = FindName("SpotifyBar3") as FrameworkElement;

            bar1?.BeginAnimation(HeightProperty, null);
            bar2?.BeginAnimation(HeightProperty, null);
            bar3?.BeginAnimation(HeightProperty, null);
        }

        public void FadeIn()
        {
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, fadeIn);
        }

        public void FadeOut(Action? onComplete = null)
        {
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            if (onComplete != null)
                fadeOut.Completed += (s, e) => onComplete();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}