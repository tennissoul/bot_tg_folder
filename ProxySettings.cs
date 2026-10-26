using System;
using System.Net;

namespace KeyBot
{
    public class ProxySettings
    {
        public bool Enabled { get; set; }
        public string? Url { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }

        public static ProxySettings Load(string configPath)
        {
            var settings = new ProxySettings { Enabled = false };

            if (string.IsNullOrWhiteSpace(configPath) || !System.IO.File.Exists(configPath))
                return settings;

            foreach (var rawLine in System.IO.File.ReadAllLines(configPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                    continue;

                var sep = line.IndexOf('=');
                if (sep < 0)
                    continue;

                var key = line[..sep].Trim().ToLowerInvariant();
                var value = line[(sep + 1)..].Trim();

                switch (key)
                {
                    case "proxy":
                        settings.Enabled = value.Equals("on", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "url":
                        settings.Url = value;
                        break;
                    case "username":
                        settings.Username = value;
                        break;
                    case "password":
                        settings.Password = value;
                        break;
                }
            }

            return settings;
        }

        /// <summary>
        /// Создаёт <see cref="WebProxy"/> на основе настроек.
        /// Поддерживает формат url = http://user:pass@host:port,
        /// а также отдельные поля username / password.
        /// </summary>
        public WebProxy? CreateWebProxy()
        {
            if (!Enabled || string.IsNullOrWhiteSpace(Url))
                return null;

            var proxy = new WebProxy();

            // Парсим credentials из URL (http://user:pass@host:port)
            if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            {
                var cleanUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
                proxy.Address = new Uri(cleanUrl);

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var infoParts = uri.UserInfo.Split(':', 2);
                    proxy.Credentials = new NetworkCredential(
                        Uri.UnescapeDataString(infoParts[0]),
                        infoParts.Length > 1 ? Uri.UnescapeDataString(infoParts[1]) : ""
                    );
                }
            }
            else
            {
                proxy.Address = new Uri(Url);
            }

            // Явно заданные username/password перекрывают URL-credentials
            if (!string.IsNullOrEmpty(Username))
            {
                proxy.Credentials = new NetworkCredential(Username, Password ?? "");
            }

            return proxy;
        }
    }
}
