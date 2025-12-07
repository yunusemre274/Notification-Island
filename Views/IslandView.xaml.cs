using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NI.Services;
using NI.ViewModels;
using NI.Views.Controls;

namespace NI.Views
{
    public enum PanelType { None, Wifi, Sound, ControlCenter }

    public partial class IslandView : UserControl
    {
        private IslandVM _vm = null!;
        private bool _isExpanded = false;
        private bool _isHovering = false;
        
        // Panel state
        private PanelType _currentPanel = PanelType.None;
        private WifiPanel? _wifiPanel;
        private SoundPanel? _soundPanel;

        // Event for MainWindow to handle Control Center
        public event EventHandler<PanelType>? PanelRequested;

        // Animation durations - FASTER for snappy HyperOS feel
        private static readonly Duration ExpandDuration = TimeSpan.FromMilliseconds(130);
        private static readonly Duration CompactDuration = TimeSpan.FromMilliseconds(110);

        // Sizes
        private const double CompactWidth = 350;
        private const double CompactHeight = 36;
        private const double ExpandedWidth = 680;
        private const double ExpandedHeight = 50;

        public IslandView()
        {
            InitializeComponent();
            _vm = (IslandVM)DataContext;
            _vm.NotificationArrived += OnNotificationArrived;
            _vm.SmartEventArrived += OnSmartEventArrived;
            _vm.SpotifyChanged += OnSpotifyChanged;
            _vm.HeadphoneBannerArrived += OnHeadphoneBannerArrived;
            _vm.ActiveWindowChanged += OnActiveWindowChanged;
            _vm.Start();
        }

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
            _isHovering = true;
            Expand();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isHovering = false;
            // Reduced delay for snappier response
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (!_isHovering && _currentPanel == PanelType.None)
                    Compact();
            };
            timer.Start();
        }

        #endregion

        #region Click Handling

        private void OnLeftClick(object sender, MouseButtonEventArgs e)
        {
            PlayPulse();
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

        #endregion

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

            Expand();
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
            Dispatcher.Invoke(() =>
            {
                Expand();
                
                // Auto-compact after 4 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(4)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    if (!_isHovering)
                        Compact();
                };
                timer.Start();
            });
        }

        private void OnSmartEventArrived(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Expand();
                
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(6)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    if (!_isHovering)
                        Compact();
                };
                timer.Start();
            });
        }

        private void OnSpotifyChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateExpandedContentVisibility();
            });
        }

        private void OnHeadphoneBannerArrived(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateExpandedContentVisibility();
                Expand();
                
                // Auto-compact after 4 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(4)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    if (!_isHovering)
                        Compact();
                };
                timer.Start();
            });
        }

        private void OnActiveWindowChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateActiveWindowIcon();
                UpdateExpandedContentVisibility();
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
                if (ActiveWindowIconExpanded != null)
                    ActiveWindowIconExpanded.Source = icon;
            }
            else
            {
                // Fallback to AppIcon
                var icon = Resources["AppIcon"] as ImageSource;
                if (ActiveWindowIconCompact != null)
                    ActiveWindowIconCompact.Source = icon;
                if (ActiveWindowIconExpanded != null)
                    ActiveWindowIconExpanded.Source = icon;
            }
        }

        private void UpdateExpandedContentVisibility()
        {
            // Priority: HeadphoneBanner > Spotify > ActiveWindow > Notification
            if (_vm.ShowHeadphoneBanner)
            {
                HeadphoneBannerExpanded.Visibility = Visibility.Visible;
                SpotifyContent.Visibility = Visibility.Collapsed;
                ActiveWindowExpanded.Visibility = Visibility.Collapsed;
                NotificationContent.Visibility = Visibility.Collapsed;
            }
            else if (_vm.IsSpotifyPlaying)
            {
                HeadphoneBannerExpanded.Visibility = Visibility.Collapsed;
                SpotifyContent.Visibility = Visibility.Visible;
                ActiveWindowExpanded.Visibility = Visibility.Collapsed;
                NotificationContent.Visibility = Visibility.Collapsed;
            }
            else if (_vm.ShowActiveWindow)
            {
                HeadphoneBannerExpanded.Visibility = Visibility.Collapsed;
                SpotifyContent.Visibility = Visibility.Collapsed;
                ActiveWindowExpanded.Visibility = Visibility.Visible;
                NotificationContent.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeadphoneBannerExpanded.Visibility = Visibility.Collapsed;
                SpotifyContent.Visibility = Visibility.Collapsed;
                ActiveWindowExpanded.Visibility = Visibility.Collapsed;
                NotificationContent.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Animations - FAST HyperOS Style

        private void Expand()
        {
            if (_isExpanded) return;
            _isExpanded = true;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var widthAnim = new DoubleAnimation(ExpandedWidth, ExpandDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(WidthProperty, widthAnim);

            var heightAnim = new DoubleAnimation(ExpandedHeight, ExpandDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(HeightProperty, heightAnim);

            IslandBorder.CornerRadius = new CornerRadius(25);

            // Faster content transitions
            var fadeOutCompact = new DoubleAnimation(0, TimeSpan.FromMilliseconds(50));
            var fadeInExpanded = new DoubleAnimation(1, TimeSpan.FromMilliseconds(80)) 
            { 
                BeginTime = TimeSpan.FromMilliseconds(40) 
            };

            CompactContent.BeginAnimation(OpacityProperty, fadeOutCompact);
            ExpandedContent.BeginAnimation(OpacityProperty, fadeInExpanded);
            ExpandedContent.IsHitTestVisible = true;
        }

        private void Compact()
        {
            if (!_isExpanded) return;
            _isExpanded = false;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var widthAnim = new DoubleAnimation(CompactWidth, CompactDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(WidthProperty, widthAnim);

            var heightAnim = new DoubleAnimation(CompactHeight, CompactDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(HeightProperty, heightAnim);

            IslandBorder.CornerRadius = new CornerRadius(18);

            // Faster content transitions
            var fadeOutExpanded = new DoubleAnimation(0, TimeSpan.FromMilliseconds(50));
            var fadeInCompact = new DoubleAnimation(1, TimeSpan.FromMilliseconds(70)) 
            { 
                BeginTime = TimeSpan.FromMilliseconds(30) 
            };

            ExpandedContent.BeginAnimation(OpacityProperty, fadeOutExpanded);
            ExpandedContent.IsHitTestVisible = false;
            CompactContent.BeginAnimation(OpacityProperty, fadeInCompact);
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
    }
}
