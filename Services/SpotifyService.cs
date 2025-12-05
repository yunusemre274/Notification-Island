using System;
using System.Diagnostics;
using NI.Models;

namespace NI.Services
{
    /// <summary>
    /// Lightweight Spotify integration using window title detection.
    /// OPTIMIZED: No internal timer - called on-demand by main timer.
    /// Only fires events when track actually changes.
    /// </summary>
    public class SpotifyService
    {
        private SpotifyTrack? _lastTrack;
        private string _lastTitle = "";

        public event EventHandler<SpotifyTrack?>? TrackChanged;

        public SpotifyTrack? CurrentTrack => _lastTrack;

        /// <summary>
        /// Called by main timer. Only fires event if track changed.
        /// </summary>
        public void CheckSpotify()
        {
            var track = GetCurrentTrack();
            
            // Only fire event if track changed
            bool changed = false;
            if (track == null && _lastTrack != null)
                changed = true;
            else if (track != null && _lastTrack == null)
                changed = true;
            else if (track != null && _lastTrack != null)
                changed = track.Song != _lastTrack.Song || track.Artist != _lastTrack.Artist;

            if (changed)
            {
                _lastTrack = track;
                TrackChanged?.Invoke(this, track);
            }
        }

        /// <summary>
        /// Gets current track from Spotify window title.
        /// Spotify format: "Song - Artist" or "Spotify Premium"/"Spotify Free" when paused.
        /// </summary>
        private SpotifyTrack? GetCurrentTrack()
        {
            try
            {
                var processes = Process.GetProcessesByName("Spotify");
                if (processes.Length == 0) return null;

                // Find the main Spotify window (has a title)
                string? title = null;
                foreach (var p in processes)
                {
                    if (!string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    {
                        title = p.MainWindowTitle;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(title)) return null;

                // Same title as last check - skip parsing
                if (title == _lastTitle && _lastTrack != null)
                    return _lastTrack;

                _lastTitle = title;

                // "Spotify Premium", "Spotify Free", "Spotify" = no song playing
                if (title.StartsWith("Spotify", StringComparison.OrdinalIgnoreCase) && !title.Contains(" - "))
                    return null;

                // Format: "Song - Artist"
                var separatorIndex = title.IndexOf(" - ");
                if (separatorIndex <= 0) return null;

                return new SpotifyTrack
                {
                    Song = title.Substring(0, separatorIndex).Trim(),
                    Artist = title.Substring(separatorIndex + 3).Trim(),
                    IsPlaying = true
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Opens Spotify application.
        /// </summary>
        public static void OpenSpotify()
        {
            try
            {
                Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("Spotify.exe") { UseShellExecute = true });
                }
                catch { }
            }
        }
    }
}
