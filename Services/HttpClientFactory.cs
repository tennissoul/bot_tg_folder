using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace KeyBot.Services;

/// <summary>Фабрика для создания HttpClient с поддержкой прокси.</summary>
public static class HttpClientFactory
{
    public static HttpClient BuildHttpClient()
    {
        var proxyConfigPath = FindProxyConfig();
        var proxy = ProxySettings.Load(proxyConfigPath).CreateWebProxy();

        if (proxy != null && ProxyIsReachable(proxy))
        {
            Console.WriteLine($"✓ Используется прокси: {proxy.Address}");
            return new HttpClient(
                new SocketsHttpHandler
                {
                    UseProxy = true,
                    Proxy = proxy,
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                }
            )
            {
                Timeout = TimeSpan.FromSeconds(180),
            };
        }

        if (proxy != null)
            Console.WriteLine($"⚠️ Прокси {proxy.Address} недоступен — прямое подключение");
        else
            Console.WriteLine("✓ Прямое подключение");

        return new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(180),
        };
    }

    private static string FindProxyConfig()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "proxy.config"),
            Path.Combine(Directory.GetCurrentDirectory(), "proxy.config"),
        };
        foreach (var p in candidates)
            if (System.IO.File.Exists(p))
                return p;
        return candidates[0];
    }

    private static bool ProxyIsReachable(WebProxy proxy)
    {
        try
        {
            var uri = proxy.Address!;
            using var tcp = new TcpClient();
            return tcp.ConnectAsync(uri.Host, uri.Port).Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            return false;
        }
    }
}
