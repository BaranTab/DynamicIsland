using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace DynamicIsland.Modules;

public sealed record HourlyForecast(string Hour, string Icon, double Temperature);

/// <summary>
/// Open-Meteo API'si ile güncel hava durumu ve saatlik tahmini çeker (ücretsiz, API key gerekmez).
/// </summary>
public sealed class WeatherModule : IIslandModule
{
    private static readonly HttpClient Http = new();
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromMinutes(15));
    private readonly double _latitude;
    private readonly double _longitude;

    /// <param name="latitude">Varsayılan: Karabük</param>
    public WeatherModule(double latitude = 41.20, double longitude = 32.62, string locationName = "Karabük")
    {
        _latitude = latitude;
        _longitude = longitude;
        LocationName = locationName;
    }

    public string Name => "Hava Durumu";
    public int Priority => 5;
    public bool IsActive => Temperature.HasValue;

    public string LocationName { get; }
    public double? Temperature { get; private set; }
    public double? MaxTemperature { get; private set; }
    public double? MinTemperature { get; private set; }
    public int WeatherCode { get; private set; }
    public string Icon => ToIcon(WeatherCode);
    public string Description => ToDescription(WeatherCode);
    public IReadOnlyList<HourlyForecast> Hourly { get; private set; } = Array.Empty<HourlyForecast>();

    public event EventHandler? Changed;

    public async Task StartAsync()
    {
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.Start();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var lat = _latitude.ToString(CultureInfo.InvariantCulture);
            var lon = _longitude.ToString(CultureInfo.InvariantCulture);
            var url = "https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={lat}&longitude={lon}" +
                      "&current=temperature_2m,weather_code" +
                      "&hourly=temperature_2m,weather_code" +
                      "&daily=temperature_2m_max,temperature_2m_min" +
                      "&timezone=auto&forecast_days=2";

            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var root = doc.RootElement;

            var current = root.GetProperty("current");
            Temperature = current.GetProperty("temperature_2m").GetDouble();
            WeatherCode = current.GetProperty("weather_code").GetInt32();
            var now = DateTime.Parse(current.GetProperty("time").GetString()!, CultureInfo.InvariantCulture);

            var daily = root.GetProperty("daily");
            MaxTemperature = daily.GetProperty("temperature_2m_max")[0].GetDouble();
            MinTemperature = daily.GetProperty("temperature_2m_min")[0].GetDouble();

            // Şu andan sonraki 5 saat
            var hourly = root.GetProperty("hourly");
            var times = hourly.GetProperty("time");
            var temps = hourly.GetProperty("temperature_2m");
            var codes = hourly.GetProperty("weather_code");

            var list = new List<HourlyForecast>();
            for (int i = 0; i < times.GetArrayLength() && list.Count < 5; i++)
            {
                var t = DateTime.Parse(times[i].GetString()!, CultureInfo.InvariantCulture);
                if (t <= now) continue;
                list.Add(new HourlyForecast(t.ToString("HH:mm"), ToIcon(codes[i].GetInt32()), temps[i].GetDouble()));
            }
            Hourly = list;
        }
        catch
        {
            // İnternet yoksa son bilinen değer kalır.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // WMO hava durumu kodları: https://open-meteo.com/en/docs
    public static string ToIcon(int code) => code switch
    {
        0 => "☀",
        1 or 2 or 3 => "⛅",
        45 or 48 => "🌫",
        >= 51 and <= 57 => "🌦",
        (>= 61 and <= 67) or (>= 80 and <= 82) => "🌧",
        (>= 71 and <= 77) or 85 or 86 => "❄",
        >= 95 => "⛈",
        _ => "☁"
    };

    public static string ToDescription(int code) => code switch
    {
        0 => "Açık",
        1 or 2 or 3 => "Parçalı bulutlu",
        45 or 48 => "Sisli",
        >= 51 and <= 57 => "Çisenti",
        (>= 61 and <= 67) or (>= 80 and <= 82) => "Yağmurlu",
        (>= 71 and <= 77) or 85 or 86 => "Karlı",
        >= 95 => "Fırtına",
        _ => "Bulutlu"
    };

    public void Dispose() => _timer.Dispose();
}
