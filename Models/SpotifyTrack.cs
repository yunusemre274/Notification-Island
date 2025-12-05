namespace NI.Models
{
    /// <summary>
    /// Represents the currently playing Spotify track.
    /// </summary>
    public class SpotifyTrack
    {
        public string Song { get; set; } = "";
        public string Artist { get; set; } = "";
        public bool IsPlaying { get; set; }

        /// <summary>
        /// Song name only for display (no artist).
        /// </summary>
        public string DisplayText => Song;
        
        /// <summary>
        /// Compact text for small view - song name only, truncated.
        /// </summary>
        public string CompactText
        {
            get
            {
                return Song.Length > 25 ? Song.Substring(0, 22) + "..." : Song;
            }
        }
    }
}
