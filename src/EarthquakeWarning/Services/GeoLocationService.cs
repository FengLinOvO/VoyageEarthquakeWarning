using System.Text.Json;

namespace Voyage.EarthquakeWarning.Services;

public sealed class GeoLocationService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<(bool Success, double Lat, double Lng, string Message)> LocateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://ip9.com.cn/get", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var data = json.RootElement.GetProperty("data");
            var lat = double.Parse(data.GetProperty("lat").GetString()!);
            var lng = double.Parse(data.GetProperty("lng").GetString()!);
            return (true, lat, lng, $"{data.GetProperty("prov").GetString()} {data.GetProperty("city").GetString()}");
        }
        catch (Exception ex)
        {
            return (false, 0, 0, ex.Message);
        }
    }
}
