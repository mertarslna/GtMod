using System;
using System.Collections.Generic;
using System.Text;

namespace GtProxyMod.Protocol
{
    public class TextPacket
    {
        public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string RawText { get; set; } = string.Empty;

        public string this[string key]
        {
            get => Fields.TryGetValue(key, out var val) ? val : string.Empty;
            set => Fields[key] = value;
        }

        public TextPacket() { }

        public TextPacket(string rawText)
        {
            RawText = rawText;
            Parse(rawText);
        }

        public void Parse(string text)
        {
            Fields.Clear();
            RawText = text;

            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var cleanLine = line.Trim('\r', '\0');
                if (string.IsNullOrWhiteSpace(cleanLine))
                    continue;

                if (cleanLine.StartsWith('|'))
                {
                    cleanLine = cleanLine.Substring(1);
                }

                var pipeIndex = cleanLine.IndexOf('|');
                if (pipeIndex > 0)
                {
                    var key = cleanLine.Substring(0, pipeIndex);
                    var val = cleanLine.Substring(pipeIndex + 1);
                    Fields[key] = val;
                }
            }
        }

        public string Get(string key, string defaultValue = "")
        {
            return Fields.TryGetValue(key, out var val) ? val : defaultValue;
        }

        public void Set(string key, string value)
        {
            Fields[key] = value;
        }

        public string Build()
        {
            var sb = new StringBuilder();
            foreach (var kvp in Fields)
            {
                sb.Append(kvp.Key);
                sb.Append('|');
                sb.Append(kvp.Value);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public byte[] ToBytes(NetMessageType messageType)
        {
            var text = Build();
            var textBytes = Encoding.UTF8.GetBytes(text);
            var result = new byte[4 + textBytes.Length + 1]; // +1 for null terminator

            uint typeVal = (uint)messageType;
            result[0] = (byte)(typeVal & 0xFF);
            result[1] = (byte)((typeVal >> 8) & 0xFF);
            result[2] = (byte)((typeVal >> 16) & 0xFF);
            result[3] = (byte)((typeVal >> 24) & 0xFF);

            Buffer.BlockCopy(textBytes, 0, result, 4, textBytes.Length);
            result[^1] = 0; // Null terminator
            return result;
        }

        public static TextPacket? FromBytes(byte[] data)
        {
            if (data == null || data.Length <= 4)
                return null;

            int len = data.Length - 4;
            // Trim trailing null characters
            while (len > 0 && data[4 + len - 1] == 0)
            {
                len--;
            }

            var str = Encoding.UTF8.GetString(data, 4, len);
            return new TextPacket(str);
        }

        public override string ToString() => RawText;
    }
}
