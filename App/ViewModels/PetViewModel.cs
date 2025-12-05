using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NI.App.Services;

namespace NI.App.ViewModels
{
    public enum PetState { Idle, ClickReaction, Feeding, Jumping, Sleeping }

    public class PetViewModel : INotifyPropertyChanged
    {
        private Image? _imageControl;
        private DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
        private int _frame = 0;
        private PetStateMachine _stateMachine = new();
        private DateTime _lastInteraction = DateTime.Now;
        private DispatcherTimer _sleepTimer = new() { Interval = TimeSpan.FromMinutes(10) };

        public PetState State => _stateMachine.CurrentState;

        public PetViewModel()
        {
            _idleTimer.Tick += (s, e) => UpdateFrame();
            _sleepTimer.Tick += (s, e) => CheckSleep();
        }

        public void AttachImageControl(Image img)
        {
            _imageControl = img;
        }

        public void Start()
        {
            _idleTimer.Start();
            _sleepTimer.Start();
        }

        private void UpdateFrame()
        {
            _frame = (_frame + 1) % 4;
            var state = _stateMachine.CurrentState;
            var prefix = state switch
            {
                PetState.Sleeping => "sleep",
                PetState.Jumping => "jump",
                PetState.Feeding => "eat",
                _ => "idle"
            };
            try
            {
                var uri = new Uri($"pack://siteoforigin:,,,/Assets/PetSprites/{prefix}_{(_frame % 2) + 1}.png");
                var img = new BitmapImage(uri);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_imageControl != null)
                        _imageControl.Source = img;
                });
            }
            catch { /* Missing sprite, ignore */ }
        }

        private void CheckSleep()
        {
            if ((DateTime.Now - _lastInteraction).TotalMinutes >= 10)
            {
                _stateMachine.TriggerSleep();
            }
        }

        public void Click()
        {
            _lastInteraction = DateTime.Now;
            _stateMachine.TriggerClick();
        }

        public void Jump()
        {
            _lastInteraction = DateTime.Now;
            _stateMachine.TriggerJump();
        }

        public void Feed()
        {
            _lastInteraction = DateTime.Now;
            _stateMachine.TriggerFeed();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
