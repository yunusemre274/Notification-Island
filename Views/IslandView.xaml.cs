using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NI.ViewModels;

namespace NI.Views
{
    public partial class IslandView : UserControl
    {
        private IslandVM _vm = null!;
        private bool _isExpanded = false;
        private bool _isHovering = false;

        // Animation durations
        private static readonly Duration ExpandDuration = TimeSpan.FromMilliseconds(250);
        private static readonly Duration CompactDuration = TimeSpan.FromMilliseconds(200);

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
            _vm.Start();
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
            // Delay compact slightly to prevent flicker
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (!_isHovering)
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
            Application.Current.Shutdown();
        }

        #endregion

        #region Notification Handling

        private void OnNotificationArrived(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Expand();
                
                // Auto-compact after 4 seconds if not hovering
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

        #endregion

        #region Animations

        private void Expand()
        {
            if (_isExpanded) return;
            _isExpanded = true;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Width animation
            var widthAnim = new DoubleAnimation(ExpandedWidth, ExpandDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(WidthProperty, widthAnim);

            // Height animation
            var heightAnim = new DoubleAnimation(ExpandedHeight, ExpandDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(HeightProperty, heightAnim);

            // Corner radius
            IslandBorder.CornerRadius = new CornerRadius(25);

            // Fade content
            var fadeOutCompact = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            var fadeInExpanded = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)) 
            { 
                BeginTime = TimeSpan.FromMilliseconds(100) 
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

            // Width animation
            var widthAnim = new DoubleAnimation(CompactWidth, CompactDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(WidthProperty, widthAnim);

            // Height animation
            var heightAnim = new DoubleAnimation(CompactHeight, CompactDuration) { EasingFunction = ease };
            IslandBorder.BeginAnimation(HeightProperty, heightAnim);

            // Corner radius
            IslandBorder.CornerRadius = new CornerRadius(18);

            // Fade content
            var fadeOutExpanded = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            var fadeInCompact = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)) 
            { 
                BeginTime = TimeSpan.FromMilliseconds(50) 
            };

            ExpandedContent.BeginAnimation(OpacityProperty, fadeOutExpanded);
            ExpandedContent.IsHitTestVisible = false;
            CompactContent.BeginAnimation(OpacityProperty, fadeInCompact);
        }

        private void PlayPulse()
        {
            var scaleUp = new DoubleAnimation(1.04, TimeSpan.FromMilliseconds(80));
            var scaleDown = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(80)) 
            { 
                BeginTime = TimeSpan.FromMilliseconds(80) 
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
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);
        }

        public void FadeOut(Action? onComplete = null)
        {
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            if (onComplete != null)
                fadeOut.Completed += (s, e) => onComplete();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion
    }
}
