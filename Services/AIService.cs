using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Woobly.Models;

namespace Woobly.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";
        private const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";

        private sealed class ProviderConfig
        {
            public required string Name { get; init; }
            public required string Endpoint { get; init; }
            public required bool AddOpenRouterHeaders { get; init; }
        }

        private static readonly Dictionary<string, ProviderConfig> ProviderConfigs =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["OpenRouter"] = new ProviderConfig
                {
                    Name = "OpenRouter",
                    Endpoint = OpenRouterUrl,
                    AddOpenRouterHeaders = true
                },
                ["Groq"] = new ProviderConfig
                {
                    Name = "Groq",
                    Endpoint = GroqUrl,
                    AddOpenRouterHeaders = false
                }
            };

        public AIService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        /// <summary>
        /// Streams a chat completion response token by token via SSE.
        /// Supports full conversation history for context-aware replies.
        /// </summary>
        public async Task StreamResponseAsync(
            string provider,
            string? apiKey,
            string model,
            IEnumerable<(string role, string content)> messages,
            Action<string> onToken,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            {
                onToken("Please configure AI settings first.");
                return;
            }

            if (!ProviderConfigs.TryGetValue(provider ?? string.Empty, out var providerConfig))
            {
                onToken($"Unsupported AI provider: {provider}");
                return;
            }

            try
            {
                var requestBody = new
                {
                    model,
                    stream = true,
                    messages = messages
                        .Select(m => new { role = m.role, content = m.content })
                        .ToArray()
                };

                var json = JsonConvert.SerializeObject(requestBody);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, providerConfig.Endpoint);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
                if (providerConfig.AddOpenRouterHeaders)
                {
                    httpRequest.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/woobly");
                    httpRequest.Headers.TryAddWithoutValidation("X-Title", "Woobly");
                }

                using var response = await _httpClient.SendAsync(
                    httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync(ct);
                    try
                    {
                        var errorJson = JObject.Parse(errorText);
                        var errorMsg = errorJson["error"]?["message"]?.Value<string>();
                        onToken($"Error ({response.StatusCode}): {errorMsg ?? errorText}");
                    }
                    catch
                    {
                        onToken($"Error ({response.StatusCode}): {errorText}");
                    }
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!line.StartsWith("data: ")) continue;

                    var data = line["data: ".Length..].Trim();
                    if (data == "[DONE]") break;

                    try
                    {
                        var chunk = JObject.Parse(data);
                        var token = chunk["choices"]?[0]?["delta"]?["content"]?.Value<string>();
                        if (token != null)
                            onToken(token);
                    }
                    catch { /* malformed SSE chunk — skip */ }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                onToken($"\nError: {ex.Message}");
            }
        }

        /// <summary>
        /// Backward-compatible overload for existing OpenRouter call sites.
        /// </summary>
        public Task StreamResponseAsync(
            string? apiKey,
            string model,
            IEnumerable<(string role, string content)> messages,
            Action<string> onToken,
            CancellationToken ct = default)
        {
            return StreamResponseAsync("OpenRouter", apiKey, model, messages, onToken, ct);
        }

        /// <summary>
        /// Cleans up markdown artifacts from a completed response string.
        /// </summary>
        public static string Sanitize(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return response;

            // Normalise line endings
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\r\n|\r", "\n");

            // Collapse 3+ blank lines to one blank line
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\n{3,}", "\n\n");

            // Remove markdown code fences (```lang or ```)
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```[\w]*\n?", "");

            // Remove bold/italic markers
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\*{1,3}([^*]+)\*{1,3}", "$1");
            response = System.Text.RegularExpressions.Regex.Replace(response, @"_{1,2}([^_]+)_{1,2}", "$1");

            // Remove stray lone quotes used as emphasis: "Word" → Word
            response = System.Text.RegularExpressions.Regex.Replace(response, @"""([^""\n]+)""", "$1");

            // Convert markdown bullets (* / - at line start) to •
            response = System.Text.RegularExpressions.Regex.Replace(response, @"^\* ", "• ",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"^- ", "• ",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"^\d+\. ", "• ",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            // Convert markdown headers (### / ## / #) to plain uppercase line
            response = System.Text.RegularExpressions.Regex.Replace(response, @"^#{1,6}\s*(.+)$", "$1",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            // Trim each line
            var lines = response.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();
            response = string.Join("\n", lines);

            // Collapse multiple spaces
            response = System.Text.RegularExpressions.Regex.Replace(response, @" {2,}", " ");

            return response.Trim();
        }
    }
}
