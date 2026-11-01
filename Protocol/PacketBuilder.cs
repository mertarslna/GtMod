using System;
using System.Text;

namespace GtProxyMod.Protocol
{
    public static class PacketBuilder
    {
        public static TankPacket BuildCallFunction(VariantList variantList, int netId = -1)
        {
            byte[] encodedVariants = variantList.Encode();
            var header = new TankPacketHeader
            {
                Type = (byte)TankPacketType.CallFunction,
                NetId = netId,
                PacketFlags = 0x8,
                DataSize = (uint)encodedVariants.Length
            };

            return new TankPacket(header, encodedVariants);
        }

        public static TankPacket BuildConsoleMessage(string message)
        {
            var vl = new VariantList("OnConsoleMessage");
            vl.AddString(message);
            return BuildCallFunction(vl);
        }

        public static TankPacket BuildTextOverlay(string message)
        {
            var vl = new VariantList("OnTextOverlay");
            vl.AddString(message);
            return BuildCallFunction(vl);
        }

        public static TankPacket BuildDialog(string dialogContent)
        {
            var vl = new VariantList("OnDialogRequest");
            vl.AddString(dialogContent);
            return BuildCallFunction(vl);
        }

        public static TankPacket BuildTalkBubble(int netId, string message)
        {
            var vl = new VariantList("OnTalkBubble");
            vl.AddInt(netId);
            vl.AddString(message);
            return BuildCallFunction(vl, netId);
        }

        public static TankPacket BuildSendToServer(int port, int token, int userId, string serverString)
        {
            var vl = new VariantList("OnSendToServer");
            vl.AddInt(port);
            vl.AddInt(token);
            vl.AddInt(userId);
            vl.AddString(serverString);
            return BuildCallFunction(vl);
        }

        public static byte[] BuildGenericText(string text)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            var buffer = new byte[4 + textBytes.Length + 1];

            uint typeVal = (uint)NetMessageType.GenericText;
            buffer[0] = (byte)(typeVal & 0xFF);
            buffer[1] = (byte)((typeVal >> 8) & 0xFF);
            buffer[2] = (byte)((typeVal >> 16) & 0xFF);
            buffer[3] = (byte)((typeVal >> 24) & 0xFF);

            Buffer.BlockCopy(textBytes, 0, buffer, 4, textBytes.Length);
            buffer[^1] = 0;
            return buffer;
        }

        public static byte[] BuildGameMessage(string message)
        {
            var textBytes = Encoding.UTF8.GetBytes(message);
            var buffer = new byte[4 + textBytes.Length + 1];

            uint typeVal = (uint)NetMessageType.GameMessage;
            buffer[0] = (byte)(typeVal & 0xFF);
            buffer[1] = (byte)((typeVal >> 8) & 0xFF);
            buffer[2] = (byte)((typeVal >> 16) & 0xFF);
            buffer[3] = (byte)((typeVal >> 24) & 0xFF);

            Buffer.BlockCopy(textBytes, 0, buffer, 4, textBytes.Length);
            buffer[^1] = 0;
            return buffer;
        }

        public static TankPacket BuildTileChange(int tileX, int tileY, int itemNetId = 18, int netId = 0)
        {
            var header = new TankPacketHeader
            {
                Type = (byte)TankPacketType.TileChangeReq,
                NetId = netId,
                IntX = tileX,
                IntY = tileY,
                ItemNetId = itemNetId
            };
            return new TankPacket(header);
        }

        public static TankPacket BuildCollectObject(int objectId, float posX = 0, float posY = 0, int netId = -1)
        {
            var header = new TankPacketHeader
            {
                Type = (byte)TankPacketType.ItemActivateObjectReq,
                NetId = netId,
                IntData = objectId,
                VecX = posX,
                VecY = posY,
                PacketFlags = 0x4000
            };
            return new TankPacket(header);
        }

        public static TankPacket BuildState(float posX, float posY, int netId = 0, int flags = 0)
        {
            var header = new TankPacketHeader
            {
                Type = (byte)TankPacketType.State,
                NetId = netId,
                VecX = posX,
                VecY = posY,
                PacketFlags = flags
            };
            return new TankPacket(header);
        }
    }
}
