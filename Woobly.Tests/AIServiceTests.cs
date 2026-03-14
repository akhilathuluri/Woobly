using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Woobly.Services;
using Xunit;

namespace Woobly.Tests;

public class AIServiceTests
{
    [Fact]
    public async Task OpenRouter_UsesOpenRouterEndpointAndHeaders()
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler);
        var service = new AIService(client);

        var output = new StringBuilder();
        await service.StreamResponseAsync(
            "OpenRouter",
            "key",
            "model",
            new List<(string role, string content)> { ("user", "hello") },
            token => output.Append(token)
        );

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
        Assert.True(handler.LastRequest.Headers.Contains("HTTP-Referer"));
        Assert.True(handler.LastRequest.Headers.Contains("X-Title"));
        Assert.Equal("Hello", output.ToString());
    }

    [Fact]
    public async Task Groq_UsesGroqEndpointWithoutOpenRouterHeaders()
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler);
        var service = new AIService(client);

        var output = new StringBuilder();
        await service.StreamResponseAsync(
            "Groq",
            "key",
            "llama",
            new List<(string role, string content)> { ("user", "hi") },
            token => output.Append(token)
        );

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
        Assert.False(handler.LastRequest.Headers.Contains("HTTP-Referer"));
        Assert.False(handler.LastRequest.Headers.Contains("X-Title"));
        Assert.Equal("Hello", output.ToString());
    }

    [Fact]
    public async Task UnsupportedProvider_ReturnsUserSafeMessage()
    {
        var service = new AIService(new HttpClient(new CapturingHandler()));
        var output = new StringBuilder();

        await service.StreamResponseAsync(
            "Unknown",
            "key",
            "model",
            new List<(string role, string content)> { ("user", "hi") },
            token => output.Append(token)
        );

        Assert.Contains("Unsupported AI provider", output.ToString(), StringComparison.Ordinal);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            const string streamPayload = "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\n" +
                                         "data: [DONE]\n\n";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(streamPayload, Encoding.UTF8, "text/event-stream")
            };
            return Task.FromResult(response);
        }
    }
}
