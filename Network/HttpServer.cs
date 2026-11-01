using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GtProxyMod.Core;

namespace GtProxyMod.Network
{
    public class HttpServer
    {
        private TcpListener? _httpsListener;
        private TcpListener? _httpListener;
        private CancellationTokenSource? _cts;
        private readonly ProxyConfig _config;
        private readonly ServerDataInfo _serverData;
        private X509Certificate2? _certificate;

        public HttpServer(ProxyConfig config, ServerDataInfo serverData)
        {
            _config = config;
            _serverData = serverData;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            // 1. Load SSL Certificate (cert.pfx) for HTTPS on Port 443
            LoadCertificate();

            // 2. Start HTTPS Listener (Port 443) - macOS Growtopia uses HTTPS!
            if (_certificate != null)
            {
                try
                {
                    _httpsListener = new TcpListener(IPAddress.Any, 443);
                    _httpsListener.Start();
                    Logger.Success("HTTPS Sunucusu port 443 üzerinde hazır (macOS istemcisi için).");
                    Task.Run(() => AcceptLoop(_httpsListener, isHttps: true, _cts.Token));
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Port 443 (HTTPS) açılamadı: {ex.Message}. (Lütfen 'sudo dotnet run' ile başlatın)");
                }
            }

            // 3. Start HTTP Listener (Port 80)
            try
            {
                _httpListener = new TcpListener(IPAddress.Any, 80);
                _httpListener.Start();
                Logger.Success("HTTP Sunucusu port 80 üzerinde hazır.");
                Task.Run(() => AcceptLoop(_httpListener, isHttps: false, _cts.Token));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Port 80 (HTTP) açılamadı: {ex.Message}.");
            }
        }

        private void LoadCertificate()
        {
            try
            {
                var candidatePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "cert.pfx"),
                    Path.Combine(Directory.GetCurrentDirectory(), "cert.pfx"),
                    Path.Combine("/Users/mertarslan/Desktop/GtMod", "cert.pfx")
                };

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        _certificate = new X509Certificate2(path, "password");
                        Logger.Success($"SSL Sertifikası yüklendi: {Path.GetFileName(path)}");
                        return;
                    }
                }
                Logger.Warning("cert.pfx bulunamadı!");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Sertifika yüklenemedi: {ex.Message}");
            }
        }

        private async Task AcceptLoop(TcpListener listener, bool isHttps, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleClientAsync(client, isHttps, token), token);
                }
                catch
                {
                    if (token.IsCancellationRequested)
                        break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, bool isHttps, CancellationToken token)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;

                    Stream stream = client.GetStream();

                    if (isHttps)
                    {
                        var sslStream = new SslStream(stream, false);
                        await sslStream.AuthenticateAsServerAsync(_certificate!);
                        stream = sslStream;
                    }

                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                    // Read HTTP request line
                    string? requestLine = await reader.ReadLineAsync(token);
                    if (string.IsNullOrEmpty(requestLine))
                        return;

                    Logger.Proxy($"HTTP/S İsteği alındı: {requestLine}");

                    // Read headers until empty line
                    string? headerLine;
                    int contentLength = 0;
                    while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync(token)))
                    {
                        if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = headerLine.Split(':');
                            if (parts.Length > 1) int.TryParse(parts[1].Trim(), out contentLength);
                        }
                    }

                    // Read body if any
                    string bodyContent = string.Empty;
                    if (contentLength > 0)
                    {
                        char[] bodyBuffer = new char[contentLength];
                        await reader.ReadBlockAsync(bodyBuffer, 0, contentLength);
                        bodyContent = new string(bodyBuffer);
                        Logger.Proxy($"HTTP POST Body: {bodyContent}");
                    }

                    // Process endpoint
                    if (requestLine.Contains("server_data.php"))
                    {
                        var responsePayload = OfficialServerResolver.GetModifiedResponse(bodyContent, _config.ListenPort, _config);
                        var bodyBytes = Encoding.UTF8.GetBytes(responsePayload);

                        var httpResponse =
                            "HTTP/1.1 200 OK\r\n" +
                            "Content-Type: text/html; charset=utf-8\r\n" +
                            $"Content-Length: {bodyBytes.Length}\r\n" +
                            "Connection: close\r\n\r\n" +
                            responsePayload;

                        var fullBytes = Encoding.UTF8.GetBytes(httpResponse);
                        await stream.WriteAsync(fullBytes, 0, fullBytes.Length, token);
                        await stream.FlushAsync(token);

                        Logger.Success($"Growtopia istemcisine yerel yönlendirme gönderildi -> 127.0.0.1:{_config.ListenPort} ({bodyBytes.Length} bayt)");

                        try
                        {
                            client.Client.Shutdown(SocketShutdown.Send);
                            await Task.Delay(100, token);
                        }
                        catch { }
                    }
                    else
                    {
                        var notFound = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                        var notFoundBytes = Encoding.UTF8.GetBytes(notFound);
                        await stream.WriteAsync(notFoundBytes, 0, notFoundBytes.Length, token);
                        await stream.FlushAsync(token);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not IOException and not SocketException)
                    {
                        Logger.Error($"HTTP/S İstek hatası: {ex.Message}");
                    }
                }
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _httpsListener?.Stop();
                _httpListener?.Stop();
            }
            catch { }
        }
    }
}
