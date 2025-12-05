using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NI.App.ViewModels;

namespace NI.App.Views
{
    public partial class DynamicIslandView : UserControl
    {
        private IslandVM _vm = null!;
        private DateTime _lastClick = DateTime.MinValue;
        private bool _isExpanded = false;

        public DynamicIslandView()
        {
            InitializeComponent();
            _vm = (IslandVM)DataContext;
            _vm.NotificationArrived += OnNotificationArrived;
            _vm.Start();
        }

        private void OnNotificationArrived(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Expand();
                // Auto-compact after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    Compact();
                };
                timer.Start();
            });
        }

        private void OnLeftClick(object sender, MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            
            // Pulse animation on any click
            PlayPulse();
            
            if ((now - _lastClick).TotalMilliseconds < 350)
            {
                // Double-click: pet jumps
                _vm.PetJump();
                PlayPetJump();
            }
            else
            {
                // Single-click: pet waves, brief expand
                _vm.PetWave();
                PlayPetWave();
                
                if (!_isExpanded)
                {
                    Expand();
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1.5)
                    };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        Compact();
                    };
                    timer.Start();
                }
            }
            _lastClick = now;
        }

        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            SettingsPopup.IsOpen = true;
        }

        private void OnQuit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        public void Expand()
        {
            if (_isExpanded) return;
            _isExpanded = true;
            var sb = (Storyboard)Resources["ExpandAnim"];
            sb.Begin(this);
        }

        public void Compact()
        {
            if (!_isExpanded) return;
            _isExpanded = false;
            var sb = (Storyboard)Resources["CompactAnim"];
            sb.Begin(this);
        }

        private void PlayPulse()
        {
            var sb = (Storyboard)Resources["PulseAnim"];
            sb.Begin(this);
        }

        private void PlayPetWave()
        {
            var sb = new Storyboard();
            var scaleX = new DoubleAnimation(1.15, TimeSpan.FromMilliseconds(100)) { AutoReverse = true };
            Storyboard.SetTarget(scaleX, PetSprite);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            sb.Children.Add(scaleX);
            sb.Begin();
        }

        private void PlayPetJump()
        {
            var sb = new Storyboard();
            var jump = new DoubleAnimation(-8, TimeSpan.FromMilliseconds(120))
            {
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(jump, PetSprite);
            Storyboard.SetTargetProperty(jump, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));
            sb.Children.Add(jump);
            sb.Begin();
        }

        public void Show()
        {
            Visibility = Visibility.Visible;
            var sb = (Storyboard)Resources["EnterAnim"];
            sb.Begin(this);
        }

        public void Hide()
        {
            // Quick fade out
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) => Visibility = Visibility.Collapsed;
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
