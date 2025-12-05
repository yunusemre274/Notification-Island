using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NI.Services;

namespace NI.Views.Controls
{
    public partial class SoundPanel : UserControl
    {
        private bool _isUpdating = false;

        public SoundPanel()
        {
            InitializeComponent();
            LoadState();
            LoadDevices();
        }

        public void AnimateIn()
        {
            var slideIn = new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);
        }

        public void AnimateOut(Action? onComplete = null)
        {
            var slideOut = new DoubleAnimation(0, -10, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            
            if (onComplete != null)
                fadeOut.Completed += (s, e) => onComplete();
            
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void LoadState()
        {
            _isUpdating = true;
            VolumeSlider.Value = AudioService.Volume;
            VolumeText.Text = $"{AudioService.Volume}%";
            UpdateVolumeIcon();
            UpdateMuteButton();
            _isUpdating = false;
        }

        private void LoadDevices()
        {
            DeviceList.Children.Clear();

            var devices = AudioService.GetOutputDevices();

            foreach (var device in devices)
            {
                var item = CreateDeviceItem(device);
                DeviceList.Children.Add(item);
            }
        }

        private Border CreateDeviceItem(AudioDevice device)
        {
            var isSelected = device.Id == AudioService.CurrentDeviceId;

            var border = new Border
            {
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x78, 0xD4)) : Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Tag = device.Id
            };

            if (!isSelected)
            {
                border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
                border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;
            }
            border.MouseLeftButtonDown += OnDeviceClick;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Device icon
            var iconText = new TextBlock
            {
                Text = device.Icon,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            // Device name
            var nameText = new TextBlock
            {
                Text = device.Name,
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            // Check mark if selected
            if (isSelected)
            {
                var checkText = new TextBlock
                {
                    Text = "✓",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(checkText, 2);
                grid.Children.Add(checkText);
            }

            border.Child = grid;
            return border;
        }

        private void OnDeviceClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string deviceId)
            {
                AudioService.SetOutputDevice(deviceId);
                LoadDevices();
            }
        }

        private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            var volume = (int)e.NewValue;
            AudioService.Volume = volume;
            VolumeText.Text = $"{volume}%";
            UpdateVolumeIcon();
        }

        private void OnMuteClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AudioService.ToggleMute();
            UpdateMuteButton();
            UpdateVolumeIcon();
        }

        private void UpdateVolumeIcon()
        {
            VolumeIcon.Text = AudioService.VolumeIcon;
        }

        private void UpdateMuteButton()
        {
            if (AudioService.IsMuted)
            {
                MuteButton.Background = new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33));
                MuteIcon.Text = "🔊";
            }
            else
            {
                MuteButton.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
                MuteIcon.Text = "🔇";
            }
        }
    }
}
