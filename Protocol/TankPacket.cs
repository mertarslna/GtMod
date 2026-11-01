using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GtProxyMod.Protocol
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TankPacketHeader
    {
        public byte Type;            // 0x00: PacketType (0=State, 1=CallFunction, etc.)
        public byte Pad1;            // 0x01
        public byte Pad2;            // 0x02
        public byte Pad3;            // 0x03
        public int NetId;            // 0x04: 32-bit NetId (-1 for global RPCs like OnSendToServer)
        public int ItemNetId;        // 0x08
        public int PacketFlags;      // 0x0C: 0x8 indicates extended data present
        public float FloatVar;       // 0x10
        public int IntData;          // 0x14
        public float VecX;           // 0x18: X coordinate
        public float VecY;           // 0x1C: Y coordinate
        public float Vec2X;          // 0x20: X velocity
        public float Vec2Y;          // 0x24: Y velocity
        public float ParticleTime;   // 0x28
        public int IntX;             // 0x2C
        public int IntY;             // 0x30
        public uint DataSize;        // 0x34: Length of extended data (total 56 bytes)
    }

    public class TankPacket
    {
        public const int HeaderSize = 56;
        public TankPacketHeader Header;
        public byte[] ExtendedData { get; set; } = Array.Empty<byte>();

        public TankPacketType Type
        {
            get => (TankPacketType)Header.Type;
            set => Header.Type = (byte)value;
        }

        public TankPacket()
        {
            Header = new TankPacketHeader();
        }

        public TankPacket(TankPacketHeader header, byte[]? extendedData = null)
        {
            Header = header;
            if (extendedData != null)
            {
                ExtendedData = extendedData;
                Header.DataSize = (uint)extendedData.Length;
                Header.PacketFlags |= 0x8; // Extended data flag
            }
        }

        public static TankPacket? FromBytes(byte[] rawData, int offset = 4)
        {
            if (rawData == null || rawData.Length < offset + HeaderSize)
                return null;

            TankPacketHeader header;
            var headerBytes = new byte[HeaderSize];
            Buffer.BlockCopy(rawData, offset, headerBytes, 0, HeaderSize);

            var handle = GCHandle.Alloc(headerBytes, GCHandleType.Pinned);
            try
            {
                header = Marshal.PtrToStructure<TankPacketHeader>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            byte[] extended = Array.Empty<byte>();
            if (header.DataSize > 0 && rawData.Length >= offset + HeaderSize + (int)header.DataSize)
            {
                extended = new byte[header.DataSize];
                Buffer.BlockCopy(rawData, offset + HeaderSize, extended, 0, (int)header.DataSize);
            }

            return new TankPacket(header, extended);
        }

        public byte[] ToBytes(NetMessageType messageType = NetMessageType.GamePacket)
        {
            Header.DataSize = (uint)(ExtendedData?.Length ?? 0);
            if (Header.DataSize > 0)
            {
                Header.PacketFlags |= 0x8;
            }

            int totalSize = 4 + HeaderSize + (int)Header.DataSize;
            byte[] result = new byte[totalSize];

            // 4-byte NetMessageType prefix
            uint typeVal = (uint)messageType;
            result[0] = (byte)(typeVal & 0xFF);
            result[1] = (byte)((typeVal >> 8) & 0xFF);
            result[2] = (byte)((typeVal >> 16) & 0xFF);
            result[3] = (byte)((typeVal >> 24) & 0xFF);

            // 56-byte TankPacketHeader
            byte[] headerBytes = new byte[HeaderSize];
            var handle = GCHandle.Alloc(headerBytes, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(Header, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }

            Buffer.BlockCopy(headerBytes, 0, result, 4, HeaderSize);

            // Extended Data
            if (ExtendedData != null && ExtendedData.Length > 0)
            {
                Buffer.BlockCopy(ExtendedData, 0, result, 4 + HeaderSize, ExtendedData.Length);
            }

            return result;
        }
    }
}
