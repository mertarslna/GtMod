namespace GtProxyMod.Core
{
    public class ProxyConfig
    {
        public ushort ListenPort { get; set; } = 17091;
        public string TargetHost { get; set; } = "213.179.209.175"; // Live official Growtopia master server
        public ushort TargetPort { get; set; } = 17048;            // Live official port
        public ushort HttpPort { get; set; } = 80;                 // Port for server_data.php redirect

        public bool AutoResolveOfficialServer { get; set; } = true;
        public bool LogPackets { get; set; } = true;
        public bool LogMovement { get; set; } = false;             // Disabled by default to prevent spam
        public bool LogVariantList { get; set; } = true;
        public bool LogTextPackets { get; set; } = true;
        public string CommandPrefix { get; set; } = "/";
    }
}
