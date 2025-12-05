using System;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NI.App.ViewModels
{
    /// <summary>
    /// Ultra-lightweight Pet ViewModel.
    /// - Idle animation runs at low FPS (2-3 fps)
    /// - Animations triggered only on events
    /// - Stops completely when hidden
    /// </summary>
    public class PetVM
    {
        private DispatcherTimer? _idleTimer;
        private int _frame = 0;
        private bool _isRunning = false;
        private PetState _state = PetState.Idle;

        public BitmapImage? CurrentFrame { get; private set; }
        public event EventHandler? FrameChanged;

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            // Very low frame rate for idle (400ms = ~2.5 fps)
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _idleTimer.Tick += OnIdleTick;
            _idleTimer.Start();
            UpdateFrame();
        }

        public void Stop()
        {
            _isRunning = false;
            _idleTimer?.Stop();
            _idleTimer = null;
        }

        private void OnIdleTick(object? sender, EventArgs e)
        {
            if (_state != PetState.Idle) return;
            _frame = (_frame + 1) % 2; // Only 2 frames for idle
            UpdateFrame();
        }

        private void UpdateFrame()
        {
            var prefix = _state switch
            {
                PetState.Wave => "wave",
                PetState.Jump => "jump",
                _ => "idle"
            };

            try
            {
                var uri = new Uri($"pack://siteoforigin:,,,/Assets/Pet/{prefix}_{(_frame % 2) + 1}.png");
                CurrentFrame = new BitmapImage(uri);
                CurrentFrame.Freeze(); // Freeze for performance
                FrameChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Missing sprite - ignore
            }
        }

        public void TriggerWave()
        {
            _state = PetState.Wave;
            _frame = 0;
            UpdateFrame();
            ScheduleReturnToIdle(400);
        }

        public void TriggerJump()
        {
            _state = PetState.Jump;
            _frame = 0;
            UpdateFrame();
            ScheduleReturnToIdle(300);
        }

        private void ScheduleReturnToIdle(int ms)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _state = PetState.Idle;
                UpdateFrame();
            };
            timer.Start();
        }

        private enum PetState { Idle, Wave, Jump }
    }
}
