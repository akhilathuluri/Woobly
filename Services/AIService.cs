using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Woobly.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

        public AIService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> GetResponseAsync(string? apiKey, string model, string userMessage)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
                return "Please configure AI settings first.";

            try
            {
                var request = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    }
                };

                var jsonContent = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/woobly");
                _httpClient.DefaultRequestHeaders.Add("X-Title", "Woobly");

                var response = await _httpClient.PostAsync(OpenRouterUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Try to parse error details
                    try
                    {
                        var errorJson = JObject.Parse(responseText);
                        var errorMessage = errorJson["error"]?["message"]?.Value<string>();
                        return $"Error ({response.StatusCode}): {errorMessage ?? responseText}";
                    }
                    catch
                    {
                        return $"Error ({response.StatusCode}): {responseText}";
                    }
                }

                var json = JObject.Parse(responseText);
                var aiResponse = json["choices"]?[0]?["message"]?["content"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(aiResponse))
                    return "No response received.";

                // Sanitize and format the response
                return SanitizeResponse(aiResponse);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private string SanitizeResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            // Remove excessive whitespace and normalize line breaks
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\r\n|\r|\n", "\n");
            
            // Remove multiple consecutive blank lines (keep max 1 blank line)
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\n{3,}", "\n\n");
            
            // Remove leading/trailing whitespace from each line
            var lines = response.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }
            response = string.Join("\n", lines);
            
            // Remove markdown code block markers for cleaner display
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```[\w]*\n?", "");
            
            // Remove excessive spaces
            response = System.Text.RegularExpressions.Regex.Replace(response, @" {2,}", " ");
            
            // Clean up markdown bold/italic for better readability
            response = response.Replace("**", "");
            response = response.Replace("__", "");
            
            // Convert markdown lists to simple format
            response = System.Text.RegularExpressions.Regex.Replace(response, @"^\* ", "• ", System.Text.RegularExpressions.RegexOptions.Multiline);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"^- ", "• ", System.Text.RegularExpressions.RegexOptions.Multiline);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"^\d+\. ", "• ", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Trim final result
            return response.Trim();
        }
    }
}
