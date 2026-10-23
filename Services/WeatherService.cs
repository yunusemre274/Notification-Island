using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace NI.Services;

public sealed class WeatherService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };
    // Explicit coordinates only: no location permission, IP lookup, or placeholder temperature.
    public async Task<string?> ReadAsync(string latitude, string longitude, CancellationToken token)
    {
        if (!double.TryParse(latitude.Replace(',', '.'), CultureInfo.InvariantCulture, out var lat) || lat < -90 || lat > 90
            || !double.TryParse(longitude.Replace(',', '.'), CultureInfo.InvariantCulture, out var lon) || lon < -180 || lon > 180
            || !double.IsFinite(lat) || !double.IsFinite(lon)) return null;
        var uri = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m");
        using var response = await _http.GetAsync(uri, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var temperature = document.RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble();
        return $"{Math.Round(temperature):0}°";
    }
    public void Dispose() => _http.Dispose();
}
