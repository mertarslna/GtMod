using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GtProxyMod.Core;

namespace GtProxyMod.Network
{
    public class ServerDataInfo
    {
        public string Server { get; set; } = "213.179.209.175"; // Live official Growtopia master server
        public ushort Port { get; set; } = 17049;               // Live official port
        public string Meta { get; set; } = "growtopia";
        public string RawResponse { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
    }

    public static class OfficialServerResolver
    {
        // Known Akamai CDN IP addresses for www.growtopia1.com to bypass /etc/hosts redirection
        private static readonly string[] AkamaiIps =
        {
            "195.175.176.192",
            "195.175.176.210",
            "23.217.138.110",
            "23.217.138.108"
        };

        private static string? _cachedOfficialResponse;
        private static DateTime _lastFetchTime = DateTime.MinValue;

        public static string GetModifiedResponse(string postBody, ushort localProxyPort, ProxyConfig config)
        {
            var raw = FetchRealServerData(postBody);
            if (string.IsNullOrEmpty(raw))
            {
                // Fallback template
                return
                    $"server|127.0.0.1\n" +
                    $"port|{localProxyPort}\n" +
                    $"loginurl|login.growtopiagame.com\n" +
                    $"type|1\n" +
                    $"meta|KQHay1OuelibWFc2kFbn6ZCh12Zi2wpXODrh3P6WlTI=\n" +
                    $"type2|1\n" +
                    $"RTENDMARKERBS1001\n";
            }

            // Parse real server and port from official response
            var lines = raw.Split('\n');
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                var clean = line.Trim('\r');
                if (clean.StartsWith("server|", StringComparison.OrdinalIgnoreCase))
                {
                    var realIp = clean.Substring(7).Trim();
                    if (!string.IsNullOrEmpty(realIp) && !realIp.StartsWith("127."))
                    {
                        config.TargetHost = realIp;
                    }
                    sb.Append("server|127.0.0.1\n");
                }
                else if (clean.StartsWith("port|", StringComparison.OrdinalIgnoreCase))
                {
                    var portStr = clean.Substring(5).Trim();
                    if (ushort.TryParse(portStr, out var realPort))
                    {
                        config.TargetPort = realPort;
                    }
                    sb.Append($"port|{localProxyPort}\n");
                }
                else if (clean.StartsWith("type2|", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("type2|1\n");
                }
                else
                {
                    sb.Append(clean);
                    sb.Append('\n');
                }
            }

            Logger.Success($"Resmi sunucu adresi güncellendi: {config.TargetHost}:{config.TargetPort}");
            return sb.ToString();
        }

        public static string FetchRealServerData(string postBody)
        {
            // Cache for 15 seconds to prevent spamming Ubisoft on client reconnect bursts
            if (_cachedOfficialResponse != null && (DateTime.UtcNow - _lastFetchTime).TotalSeconds < 15)
            {
                return _cachedOfficialResponse;
            }

            if (string.IsNullOrWhiteSpace(postBody))
            {
                postBody = "platform=2&protocol=227&version=5.6";
            }

            foreach (var ip in AkamaiIps)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "curl",
                        Arguments = $"-s -k --max-time 3 --resolve www.growtopia1.com:443:{ip} -X POST -H \"User-Agent: UbiServices_SDK_HTTP_Client_Client/1.0.0\" -d \"{postBody}\" https://www.growtopia1.com/growtopia/server_data.php",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(3000);

                        if (!string.IsNullOrWhiteSpace(output) && output.Contains("server|") && output.Contains("RTENDMARKERBS1001"))
                        {
                            _cachedOfficialResponse = output;
                            _lastFetchTime = DateTime.UtcNow;
                            Logger.Success($"Ubisoft'tan canlı sunucu verisi ve meta belirteci alındı (CDN IP: {ip})");
                            return output;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"CDN IP {ip} sorgulanamadı: {ex.Message}");
                }
            }

            return _cachedOfficialResponse ?? string.Empty;
        }

        public static async Task<ServerDataInfo> ResolveAsync()
        {
            var info = new ServerDataInfo();
            await Task.Run(() =>
            {
                var raw = FetchRealServerData("platform=2&protocol=227&version=5.6");
                if (!string.IsNullOrEmpty(raw))
                {
                    info.RawResponse = raw;
                    info.IsSuccess = true;
                    var lines = raw.Split('\n');
                    foreach (var line in lines)
                    {
                        var clean = line.Trim('\r', '\0');
                        var parts = clean.Split('|');
                        if (parts.Length >= 2)
                        {
                            if (parts[0].Equals("server", StringComparison.OrdinalIgnoreCase)) info.Server = parts[1];
                            else if (parts[0].Equals("port", StringComparison.OrdinalIgnoreCase) && ushort.TryParse(parts[1], out var p)) info.Port = p;
                            else if (parts[0].Equals("meta", StringComparison.OrdinalIgnoreCase)) info.Meta = parts[1];
                        }
                    }
                }
            });

            return info;
        }
    }
}
