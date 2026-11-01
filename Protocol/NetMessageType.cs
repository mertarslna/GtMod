namespace GtProxyMod.Protocol
{
    public enum NetMessageType : uint
    {
        Unknown = 0,
        ServerHello = 1,
        GenericText = 2,
        GameMessage = 3,
        GamePacket = 4,
        Error = 5,
        Track = 6,
        ClientLogRequest = 7,
        ClientLogResponse = 8
    }
}
