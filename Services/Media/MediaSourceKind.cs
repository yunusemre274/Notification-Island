namespace NI.Services.Media;

public enum MediaSourceKind { Generic, Spotify, YouTube }

public static class MediaSourceClassifier
{
    public static MediaSourceKind Classify(string? appId, string? albumTitle = null, string? albumArtist = null)
    {
        if (appId?.Contains("spotify", StringComparison.OrdinalIgnoreCase) == true) return MediaSourceKind.Spotify;
        if (appId?.Contains("youtube", StringComparison.OrdinalIgnoreCase) == true) return MediaSourceKind.YouTube;

        // A browser ID alone cannot identify a tab. Only use explicit publisher metadata;
        // never label every Chrome/Edge session as YouTube or inspect unrelated browser tabs.
        var browser = appId != null && new[] { "chrome", "msedge", "firefox", "brave", "opera" }
            .Any(name => appId.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (browser && (IsYouTube(albumTitle) || IsYouTube(albumArtist))) return MediaSourceKind.YouTube;
        return MediaSourceKind.Generic;
    }

    private static bool IsYouTube(string? value) => value?.Trim() is { } name &&
        (name.Equals("YouTube", StringComparison.OrdinalIgnoreCase) || name.Equals("YouTube Music", StringComparison.OrdinalIgnoreCase));
}
