using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NI.Core
{
    /// <summary>
    /// PHASE 1: Single Source of Truth for Top Bar State Management
    ///
    /// CRITICAL RULES:
    /// 1. Only ONE mode can be active at any time
    /// 2. Every feature MUST explicitly return to Idle when done
    /// 3. NO UI stacking - inactive UI must use Visibility.Collapsed
    /// 4. State changes are explicit, never implicit
    ///
    /// Priority Order:
    /// ControlPanel > SearchAnswer > Notification > SpotifyExpanded > SpotifyPill > Idle
    /// </summary>
    public class TopBarStateController : INotifyPropertyChanged
    {
        private static TopBarStateController? _instance;

        /// <summary>
        /// Singleton instance - ensures only ONE state controller exists
        /// </summary>
        public static TopBarStateController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TopBarStateController();
                }
                return _instance;
            }
        }

        private TopBarMode _currentMode = TopBarMode.Idle;

        /// <summary>
        /// Current mode - SINGLE SOURCE OF TRUTH
        /// </summary>
        public TopBarMode CurrentMode
        {
            get => _currentMode;
            private set
            {
                if (_currentMode != value)
                {
                    var oldMode = _currentMode;
                    _currentMode = value;

                    // Log state transition for debugging
                    System.Diagnostics.Debug.WriteLine($"[TopBarState] Mode Transition: {oldMode} → {_currentMode}");

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsIdle));
                    OnPropertyChanged(nameof(IsControlPanel));
                    OnPropertyChanged(nameof(IsNotification));
                    OnPropertyChanged(nameof(IsSpotifyPill));
                    OnPropertyChanged(nameof(IsSpotifyExpanded));
                    OnPropertyChanged(nameof(IsSearchAnswer));
                    OnPropertyChanged(nameof(IsAgentProcessing));
                    OnPropertyChanged(nameof(IsAgentResult));
                    OnPropertyChanged(nameof(IsChatProcessing));
                    OnPropertyChanged(nameof(IsChatAnswer));

                    // Notify subscribers
                    ModeChanged?.Invoke(this, _currentMode);
                }
            }
        }

        // XAML Binding Properties - Each returns true for ONLY ONE mode
        public bool IsIdle => CurrentMode == TopBarMode.Idle;
        public bool IsControlPanel => CurrentMode == TopBarMode.ControlPanel;
        public bool IsNotification => CurrentMode == TopBarMode.Notification;
        public bool IsSpotifyPill => CurrentMode == TopBarMode.SpotifyPill;
        public bool IsSpotifyExpanded => CurrentMode == TopBarMode.SpotifyExpanded;
        public bool IsSearchAnswer => CurrentMode == TopBarMode.SearchAnswer;
        public bool IsAgentProcessing => CurrentMode == TopBarMode.AgentProcessing;
        public bool IsAgentResult => CurrentMode == TopBarMode.AgentResult;
        public bool IsChatProcessing => CurrentMode == TopBarMode.ChatProcessing;
        public bool IsChatAnswer => CurrentMode == TopBarMode.ChatAnswer;

        /// <summary>
        /// Event fired when mode changes
        /// </summary>
        public event EventHandler<TopBarMode>? ModeChanged;

        private TopBarStateController()
        {
            // Private constructor for singleton
            // Default mode is Idle
            _currentMode = TopBarMode.Idle;
        }

        /// <summary>
        /// Sets the current mode - ONLY way to change state
        /// </summary>
        /// <param name="mode">The mode to set</param>
        public void SetMode(TopBarMode mode)
        {
            CurrentMode = mode;
        }

        /// <summary>
        /// PHASE 1: Explicitly return to Idle
        /// Called when features exit (Spotify closed, LLM answer dismissed, etc.)
        /// </summary>
        public void ReturnToIdle()
        {
            System.Diagnostics.Debug.WriteLine($"[TopBarState] EXPLICIT RETURN TO IDLE from {CurrentMode}");
            CurrentMode = TopBarMode.Idle;
        }

        /// <summary>
        /// Check if we should allow mode transition based on priority
        /// Higher priority modes cannot be overridden by lower priority ones
        /// </summary>
        public bool CanTransitionTo(TopBarMode newMode)
        {
            // ControlPanel, SearchAnswer, and AI routing modes cannot be overridden (user initiated)
            if (CurrentMode == TopBarMode.ControlPanel ||
                CurrentMode == TopBarMode.SearchAnswer ||
                CurrentMode == TopBarMode.AgentProcessing ||
                CurrentMode == TopBarMode.AgentResult ||
                CurrentMode == TopBarMode.ChatProcessing ||
                CurrentMode == TopBarMode.ChatAnswer)
            {
                // Only allow transition if explicitly returning to Idle or same priority
                if (newMode == TopBarMode.Idle)
                    return true;

                if (newMode == TopBarMode.ControlPanel ||
                    newMode == TopBarMode.SearchAnswer ||
                    newMode == TopBarMode.AgentProcessing ||
                    newMode == TopBarMode.AgentResult ||
                    newMode == TopBarMode.ChatProcessing ||
                    newMode == TopBarMode.ChatAnswer)
                    return true;

                return false; // Block lower priority transitions
            }

            // Allow all other transitions
            return true;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// PHASE 1: Top Bar Modes (KISS - One mode at a time)
    /// </summary>
    public enum TopBarMode
    {
        /// <summary>
        /// Default state: Black capsule with Time + Green Dot | Search Bar | Weather
        /// </summary>
        Idle,

        /// <summary>
        /// Control panel is open (pill stays, panel appears below)
        /// </summary>
        ControlPanel,

        /// <summary>
        /// System notification active (App logo + "1 new notification")
        /// </summary>
        Notification,

        /// <summary>
        /// Spotify playing - compact mode (Logo + Track info + Waveform)
        /// </summary>
        SpotifyPill,

        /// <summary>
        /// Spotify expanded - full card (Album art + Controls + Timeline)
        /// </summary>
        SpotifyExpanded,

        /// <summary>
        /// Search answer displayed (Large black card with Ollama response)
        /// </summary>
        SearchAnswer,

        /// <summary>
        /// Agent command processing (shows loading indicator)
        /// </summary>
        AgentProcessing,

        /// <summary>
        /// Agent command result (shows execution result)
        /// </summary>
        AgentResult,

        /// <summary>
        /// Chat request processing (shows loading indicator)
        /// </summary>
        ChatProcessing,

        /// <summary>
        /// Chat answer displayed (shows conversational response)
        /// </summary>
        ChatAnswer
    }
}
