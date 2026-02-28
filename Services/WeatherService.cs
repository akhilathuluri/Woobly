using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Woobly.Models;

namespace Woobly.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.openweathermap.org/data/2.5/weather";

        public WeatherService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<(double temperature, string? condition, string? icon)> GetWeatherAsync(string? apiKey, string city)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(city))
                return (0, "N/A", "");

            try
            {
                var url = $"{BaseUrl}?q={city}&appid={apiKey}&units=metric";
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var temp = json["main"]?["temp"]?.Value<double>() ?? 0;
                var condition = json["weather"]?[0]?["main"]?.Value<string>();
                var icon = json["weather"]?[0]?["icon"]?.Value<string>();

                return (temp, condition, icon);
            }
            catch
            {
                return (0, "N/A", "");
            }
        }
    }
}
