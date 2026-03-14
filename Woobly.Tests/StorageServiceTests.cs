using System;
using System.Collections.Generic;
using System.IO;
using Woobly.Models;
using Woobly.Services;
using Xunit;

namespace Woobly.Tests;

public class StorageServiceTests
{
    [Fact]
    public void SaveSettings_DoesNotPersistPlaintextApiKey_AndLoadsFromSecretStore()
    {
        var temp = CreateTempDirectory();
        try
        {
            var secrets = new InMemorySecretStore();
            var storage = new StorageService(temp, secrets);

            var settings = new AppSettings
            {
                AIProvider = "Groq",
                AIApiKey = "super-secret-key",
                AIModel = "llama-3.1-8b-instant",
                City = "Paris",
                HasCompletedPrivacyConsent = true,
                EnableClipboardMonitoring = true,
                EnableCallMonitoring = false
            };

            storage.SaveSettings(settings);

            var settingsFile = Path.Combine(temp, "settings.json");
            var json = File.ReadAllText(settingsFile);
            Assert.DoesNotContain("super-secret-key", json, StringComparison.Ordinal);

            var loaded = storage.LoadSettings();
            Assert.Equal("Groq", loaded.AIProvider);
            Assert.Equal("llama-3.1-8b-instant", loaded.AIModel);
            Assert.Equal("super-secret-key", loaded.AIApiKey);
            Assert.True(loaded.HasCompletedPrivacyConsent);
            Assert.True(loaded.EnableClipboardMonitoring);
            Assert.False(loaded.EnableCallMonitoring);
        }
        finally
        {
            SafeDeleteDirectory(temp);
        }
    }

    [Fact]
    public void SaveAndLoadTasks_RoundTrips()
    {
        var temp = CreateTempDirectory();
        try
        {
            var storage = new StorageService(temp, new InMemorySecretStore());
            var tasks = new List<TaskItem>
            {
                new() { Content = "one", IsCompleted = false },
                new() { Content = "two", IsCompleted = true }
            };

            storage.SaveTasks(tasks);
            var loaded = storage.LoadTasks();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("one", loaded[0].Content);
            Assert.False(loaded[0].IsCompleted);
            Assert.Equal("two", loaded[1].Content);
            Assert.True(loaded[1].IsCompleted);
        }
        finally
        {
            SafeDeleteDirectory(temp);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "woobly-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); } catch { }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public void SetSecret(string key, string value) => _secrets[key] = value;

        public string? GetSecret(string key) => _secrets.TryGetValue(key, out var value) ? value : null;

        public void DeleteSecret(string key) => _secrets.Remove(key);
    }
}
