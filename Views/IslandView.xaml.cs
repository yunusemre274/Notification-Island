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
    /// KISS Architecture: Single Island Mode (ONLY ONE ACTIVE AT A TIME)
    /// Priority: ControlPanel > Notification > SpotifyExpanded > SpotifyPill > Idle
    /// </summary>
    public enum IslandMode
    {
        Idle,              // Default: Time + Search/ActiveWindow + Status
        ControlPanel,      // Control panel expanded (highest priority when open)
        Notification,      // System notification (app logo + "1 new notification")
        SpotifyPill,       // Spotify compact: Logo + Track info
        SpotifyExpanded,   // Spotify full: Album art + Controls + Timeline
        SearchAnswer       // Ollama answer card (large black card with response)
    }

    public partial class IslandView : UserControl, INotifyPropertyChanged
    {
        private IslandVM _vm = null!;

        // KISS: SINGLE MODE PROPERTY - Only one state active at any time
        private IslandMode _currentMode = IslandMode.Idle;
        public IslandMode CurrentMode
        {
            get => _currentMode;
            private set
            {
                if (_currentMode != value)
                {
                    var oldMode = _currentMode;
                    _currentMode = value;

                    // PHASE 0: Log state transitions for debugging
                    System.Diagnostics.Debug.WriteLine($"[PHASE0] Mode Transition: {oldMode} → {_currentMode}");

                    OnPropertyChanged(nameof(CurrentMode));

                    // Update all mode-specific properties for XAML binding
                    OnPropertyChanged(nameof(IsIdle));
                    OnPropertyChanged(nameof(IsControlPanel));
                    OnPropertyChanged(nameof(IsNotification));
                    OnPropertyChanged(nameof(IsSpotifyPill));
                    OnPropertyChanged(nameof(IsSpotifyExpanded));
                    OnPropertyChanged(nameof(IsSearchAnswer));

                    // Animate size when mode changes
                    AnimateToCurrentMode();
                }
            }
        }

        // XAML Binding Properties - Each returns true for ONLY ONE mode
        public bool IsIdle => CurrentMode == IslandMode.Idle;
        public bool IsControlPanel => CurrentMode == IslandMode.ControlPanel;
        public bool IsNotification => CurrentMode == IslandMode.Notification;
        public bool IsSpotifyPill => CurrentMode == IslandMode.SpotifyPill;
        public bool IsSpotifyExpanded => CurrentMode == IslandMode.SpotifyExpanded;
        public bool IsSearchAnswer => CurrentMode == IslandMode.SearchAnswer;

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

            // Cleanup event subscriptions when unloaded
            Unloaded += OnUnloaded;

            // REMOVED: MediaSessionService initialization (now ONLY in IslandVM)
            // REMOVED: SystemMonitor and AI initialization (non-essential features)
        }

        /// <summary>
        /// Clean up event subscriptions to prevent memory leaks
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // PHASE 0: Timer cleanup removed

            if (_vm != null)
            {
                _vm.NotificationArrived -= OnNotificationArrived;
                _vm.SmartEventArrived -= OnSmartEventArrived;
                _vm.SpotifyChanged -= OnSpotifyChanged;
                _vm.HeadphoneBannerArrived -= OnHeadphoneBannerArrived;
                _vm.ActiveWindowChanged -= OnActiveWindowChanged;
                _vm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            Unloaded -= OnUnloaded;
        }

        /// <summary>
        /// KISS: SINGLE DECISION FUNCTION - Determines mode based on priority
        /// Priority: ControlPanel > SearchAnswer > Notification > SpotifyExpanded > SpotifyPill > Idle
        /// Called whenever VM state changes
        /// </summary>
        private void UpdateMode()
        {
            // Priority 1: Control Panel (user opened it)
            if (_currentPanel != PanelType.None)
            {
                CurrentMode = IslandMode.ControlPanel;
                return;
            }

            // Priority 2: Search Answer (user is viewing Ollama response)
            // Note: SearchAnswer is set directly in OnSearchKeyDown, not through UpdateMode
            // This check ensures we don't override it unless we explicitly call CloseSearchAnswer
            if (CurrentMode == IslandMode.SearchAnswer)
            {
                return; // Stay in SearchAnswer mode until explicitly closed
            }

            // Priority 3: Notification (system notification active)
            if (_vm.HasNotification)
            {
                CurrentMode = IslandMode.Notification;
                return;
            }

            // Priority 4 & 5: Spotify (expanded if clicked, pill if playing)
            if (_vm.IsSpotifyActive)
            {
                // User clicked to expand
                if (_isSpotifyExpanded)
                {
                    CurrentMode = IslandMode.SpotifyExpanded;
                    return;
                }

                // Default: show pill when playing
                CurrentMode = IslandMode.SpotifyPill;
                return;
            }

            // Default: Idle (Time + Search + Weather)
            CurrentMode = IslandMode.Idle;
        }

        // Track if user expanded Spotify manually
        private bool _isSpotifyExpanded = false;

        // PHASE 0: Notification auto-dismiss timer REMOVED - manual dismiss only

        /// <summary>
        /// Called when ViewModel properties change
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // VM state changed, recalculate mode
            if (e.PropertyName == nameof(_vm.IsSpotifyActive) ||
                e.PropertyName == nameof(_vm.HasNotification))
            {
                UpdateMode();
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
            // KISS: No hover animations in Phase 0
            // Hover effects will be added in later phases if needed
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            // KISS: No hover animations in Phase 0
        }

        #endregion

        #region Click Handling

        private void OnLeftClick(object sender, MouseButtonEventArgs e)
        {
            // KISS: Toggle Spotify expansion when Spotify is active
            if (CurrentMode == IslandMode.SpotifyPill || CurrentMode == IslandMode.SpotifyExpanded)
            {
                _isSpotifyExpanded = !_isSpotifyExpanded;
                UpdateMode();
                e.Handled = true;
            }
            else if (CurrentMode == IslandMode.Idle)
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

        /// <summary>
        /// PHASE 4: Handle search input (Enter key)
        /// </summary>
        private async void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                e.Handled = true;

                var question = SearchTextBox.Text.Trim();
                SearchTextBox.Clear();

                // Show SearchAnswer mode with "Thinking..." message
                SearchQuestionText.Text = question;
                SearchAnswerText.Text = "Thinking...";
                CurrentMode = IslandMode.SearchAnswer;

                // Call Ollama to get answer
                var answer = await _vm.GetOllamaAnswerAsync(question);

                // Display answer
                SearchAnswerText.Text = answer;
            }
            else if (e.Key == Key.Escape)
            {
                // ESC closes search answer if open, otherwise clears search box
                if (CurrentMode == IslandMode.SearchAnswer)
                {
                    CloseSearchAnswer();
                }
                else
                {
                    SearchTextBox.Clear();
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// PHASE 4: Close search answer and return to Idle
        /// </summary>
        private void OnCloseSearchAnswer(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CloseSearchAnswer();
        }

        private void CloseSearchAnswer()
        {
            SearchTextBox.Clear();
            SearchQuestionText.Text = "";
            SearchAnswerText.Text = "";
            UpdateMode(); // Will return to Idle or other active mode
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
                UpdateMode(); // Recalculate mode when control panel opens
                return;
            }

            if (_currentPanel == panelType)
            {
                CloseCurrentPanel();
                UpdateMode(); // Recalculate mode when panel closes
                return;
            }

            if (_currentPanel != PanelType.None)
            {
                CloseCurrentPanel(() =>
                {
                    OpenPanel(panelType);
                    UpdateMode(); // Recalculate mode after panel switch
                });
            }
            else
            {
                OpenPanel(panelType);
                UpdateMode(); // Recalculate mode when panel opens
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
                    PanelContainer.Children.Add(_wifiPanel);
                    _wifiPanel.AnimateIn();
                    break;

                case PanelType.Sound:
                    _soundPanel = new SoundPanel();
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
                UpdateMode(); // Recalculate mode when all panels close
            }
        }

        #endregion

        #region Notification Handling

        /// <summary>
        /// PHASE 0: Handle notification arrival - NO auto-dismiss (manual only)
        /// </summary>
        private void OnNotificationArrived(object? sender, EventArgs e)
        {
            // Notification arrived - recalculate mode (will show notification)
            UpdateMode();

            // PHASE 0: Auto-dismiss timer REMOVED - notification stays until manually dismissed or overridden
        }

        private void OnSmartEventArrived(object? sender, EventArgs e)
        {
            // Smart event arrived - recalculate mode
            UpdateMode();
        }

        private void OnSpotifyChanged(object? sender, EventArgs e)
        {
            // Spotify state changed - recalculate mode
            UpdateMode();
        }

        private void OnHeadphoneBannerArrived(object? sender, EventArgs e)
        {
            // Headphone banner - recalculate mode
            UpdateMode();
        }

        private void OnActiveWindowChanged(object? sender, EventArgs e)
        {
            // PHASE 1: Idle pill no longer shows ActiveWindow
            // ActiveWindow info removed from Idle UI (Time + Search + Weather only)
        }

        // REMOVED: UpdateExpandedContentVisibility - replaced by automatic state transitions

        #endregion

        #region Animations - Simple Mode Transitions

        /// <summary>
        /// KISS: Animates to current mode size (called automatically when CurrentMode changes)
        /// Each mode has fixed dimensions - no complex state combinations
        /// </summary>
        private void AnimateToCurrentMode()
        {
            double targetWidth, targetHeight, targetCornerRadius;

            switch (CurrentMode)
            {
                case IslandMode.Idle:
                    // Default pill: Time + Search + Status
                    targetWidth = 350;
                    targetHeight = 36;
                    targetCornerRadius = 18;
                    break;

                case IslandMode.ControlPanel:
                    // Control panel stays as pill, panel appears below
                    targetWidth = 350;
                    targetHeight = 36;
                    targetCornerRadius = 18;
                    break;

                case IslandMode.Notification:
                    // Notification pill: App logo + text
                    targetWidth = 320;
                    targetHeight = 36;
                    targetCornerRadius = 18;
                    break;

                case IslandMode.SpotifyPill:
                    // Spotify compact: Logo + Track info
                    targetWidth = 300;
                    targetHeight = 74;
                    targetCornerRadius = 20;
                    break;

                case IslandMode.SpotifyExpanded:
                    // Spotify full card: Album + Controls + Timeline
                    targetWidth = 380;
                    targetHeight = 110;
                    targetCornerRadius = 24;
                    break;

                case IslandMode.SearchAnswer:
                    // Ollama answer card (large black card)
                    targetWidth = 450;
                    targetHeight = 200;
                    targetCornerRadius = 26;
                    break;

                default:
                    targetWidth = DefaultWidth;
                    targetHeight = DefaultHeight;
                    targetCornerRadius = 18;
                    break;
            }

            // Smooth cubic easing
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Animate size
            var widthAnim = new DoubleAnimation(targetWidth, TransitionDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(WidthProperty, widthAnim);

            var heightAnim = new DoubleAnimation(targetHeight, TransitionDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(HeightProperty, heightAnim);

            // No scale transform in KISS - just size changes
            IslandScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            IslandScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            IslandScale.ScaleX = 1.0;
            IslandScale.ScaleY = 1.0;

            // Corner radius
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