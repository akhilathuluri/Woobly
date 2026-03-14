using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Woobly.Services
{
    public interface ISecretStore
    {
        void SetSecret(string key, string value);
        string? GetSecret(string key);
        void DeleteSecret(string key);
    }

    public sealed class DpapiSecretStore : ISecretStore
    {
        private readonly string _root;

        public DpapiSecretStore(string? rootFolder = null)
        {
            _root = rootFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Woobly",
                "secrets"
            );
            Directory.CreateDirectory(_root);
        }

        public void SetSecret(string key, string value)
        {
            var path = GetPath(key);
            var plain = Encoding.UTF8.GetBytes(value);
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }

        public string? GetSecret(string key)
        {
            var path = GetPath(key);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var encrypted = File.ReadAllBytes(path);
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return null;
            }
        }

        public void DeleteSecret(string key)
        {
            var path = GetPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string GetPath(string key)
        {
            var safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            return Path.Combine(_root, $"{safe}.bin");
        }
    }
}
