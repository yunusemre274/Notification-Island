using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NI.App.ViewModels;

namespace NI.App.Views
{
    public partial class PetView : UserControl
    {
        private PetViewModel _vm = null!;
        private DateTime _lastClick = DateTime.MinValue;

        public PetView()
        {
            InitializeComponent();
            _vm = (PetViewModel)DataContext;
            _vm.AttachImageControl(PetImage);
            _vm.Start();
        }

        private void OnClick(object sender, MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            if ((now - _lastClick).TotalMilliseconds < 400)
            {
                // Double-click → jump
                PlayJumpAnimation();
                _vm.Jump();
            }
            else
            {
                // Single-click → wave
                PlayWaveAnimation();
                _vm.Click();
            }
            _lastClick = now;
        }

        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            // Feed
            PlayFeedAnimation();
            _vm.Feed();
        }

        private void PlayJumpAnimation()
        {
            var sb = new Storyboard();
            var jumpUp = new DoubleAnimation(-20, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var jumpDown = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)) { BeginTime = TimeSpan.FromMilliseconds(150) };
            Storyboard.SetTarget(jumpUp, PetImage);
            Storyboard.SetTarget(jumpDown, PetImage);
            Storyboard.SetTargetProperty(jumpUp, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));
            Storyboard.SetTargetProperty(jumpDown, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));
            sb.Children.Add(jumpUp);
            sb.Children.Add(jumpDown);
            sb.Begin();
        }

        private void PlayWaveAnimation()
        {
            var sb = new Storyboard();
            var scaleX = new DoubleAnimation(1.1, TimeSpan.FromMilliseconds(100));
            var scaleXBack = new DoubleAnimation(1, TimeSpan.FromMilliseconds(100)) { BeginTime = TimeSpan.FromMilliseconds(100) };
            Storyboard.SetTarget(scaleX, PetImage);
            Storyboard.SetTarget(scaleXBack, PetImage);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            Storyboard.SetTargetProperty(scaleXBack, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            sb.Children.Add(scaleX);
            sb.Children.Add(scaleXBack);
            sb.Begin();
        }

        private void PlayFeedAnimation()
        {
            var sb = new Storyboard();
            var scaleY = new DoubleAnimation(0.9, TimeSpan.FromMilliseconds(150));
            var scaleYBack = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)) { BeginTime = TimeSpan.FromMilliseconds(150) };
            Storyboard.SetTarget(scaleY, PetImage);
            Storyboard.SetTarget(scaleYBack, PetImage);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
            Storyboard.SetTargetProperty(scaleYBack, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
            sb.Children.Add(scaleY);
            sb.Children.Add(scaleYBack);
            sb.Begin();
        }
    }
}
