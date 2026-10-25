using System.Windows.Media.Imaging;

namespace NI.Services.Media;

public interface IMediaService : IDisposable
{
    bool HasSession { get; }
    bool IsPlaying { get; }
    string SourceAppUserModelId { get; }
    MediaSourceKind SourceKind { get; }
    string TrackTitle { get; }
    string ArtistName { get; }
    BitmapImage? AlbumArtwork { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan TotalDuration { get; }
    event EventHandler? Changed;
    Task InitializeAsync();
    void SetProgressVisible(bool visible);
    Task<bool> TryPauseAsync();
    Task<bool> TogglePlayPauseAsync();
    Task<bool> TrySkipPreviousAsync();
    Task<bool> TrySkipNextAsync();
}
