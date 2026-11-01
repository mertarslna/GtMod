using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace GtProxyMod.Protocol
{
    public enum VariantType : byte
    {
        None = 0,
        Float = 1,
        String = 2,
        Vector2 = 3,
        Vector3 = 4,
        UnsignedInt = 5,
        Int = 9
    }

    public class Variant
    {
        public byte Index { get; set; }
        public VariantType Type { get; set; }
        public object? Value { get; set; }

        public Variant(byte index, VariantType type, object? value)
        {
            Index = index;
            Type = type;
            Value = value;
        }

        public string AsString() => Value?.ToString() ?? string.Empty;
        public int AsInt() => Value switch
        {
            int i => i,
            uint u => (int)u,
            float f => (int)f,
            string s => int.TryParse(s, out var v) ? v : 0,
            _ => 0
        };

        public uint AsUInt() => Value switch
        {
            uint u => u,
            int i => (uint)i,
            _ => 0
        };

        public float AsFloat() => Value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            _ => 0f
        };
    }

    public class VariantList
    {
        private readonly List<Variant> _variants = new();

        public IReadOnlyList<Variant> Variants => _variants;

        public string FunctionName
        {
            get => _variants.Count > 0 ? _variants[0].AsString() : string.Empty;
            set
            {
                if (_variants.Count > 0)
                {
                    _variants[0] = new Variant(0, VariantType.String, value);
                }
                else
                {
                    _variants.Add(new Variant(0, VariantType.String, value));
                }
            }
        }

        public VariantList()
        {
        }

        public VariantList(string functionName)
        {
            AddString(functionName);
        }

        public void AddString(string value)
        {
            byte index = (byte)_variants.Count;
            _variants.Add(new Variant(index, VariantType.String, value));
        }

        public void AddInt(int value)
        {
            byte index = (byte)_variants.Count;
            _variants.Add(new Variant(index, VariantType.Int, value));
        }

        public void AddUInt(uint value)
        {
            byte index = (byte)_variants.Count;
            _variants.Add(new Variant(index, VariantType.UnsignedInt, value));
        }

        public void AddFloat(float value)
        {
            byte index = (byte)_variants.Count;
            _variants.Add(new Variant(index, VariantType.Float, value));
        }

        public void AddVector2(float x, float y)
        {
            byte index = (byte)_variants.Count;
            _variants.Add(new Variant(index, VariantType.Vector2, (x, y)));
        }

        public Variant? Get(int index)
        {
            if (index >= 0 && index < _variants.Count)
                return _variants[index];
            return null;
        }

        public void Set(int index, object value, VariantType? type = null)
        {
            if (index < 0 || index >= _variants.Count)
                return;

            VariantType targetType = type ?? _variants[index].Type;
            _variants[index] = new Variant((byte)index, targetType, value);
        }

        public static VariantList? Decode(byte[] extendedData)
        {
            if (extendedData == null || extendedData.Length < 1)
                return null;

            try
            {
                using var ms = new MemoryStream(extendedData);
                using var reader = new BinaryReader(ms);

                byte count = reader.ReadByte();
                var list = new VariantList();

                for (int i = 0; i < count; i++)
                {
                    if (ms.Position >= ms.Length)
                        break;

                    byte index = reader.ReadByte();
                    byte typeByte = reader.ReadByte();
                    var type = (VariantType)typeByte;

                    object? value = null;
                    switch (type)
                    {
                        case VariantType.Float:
                            value = reader.ReadSingle();
                            break;

                        case VariantType.String:
                            uint len = reader.ReadUInt32();
                            byte[] strBytes = reader.ReadBytes((int)len);
                            value = Encoding.UTF8.GetString(strBytes);
                            break;

                        case VariantType.Vector2:
                            float x = reader.ReadSingle();
                            float y = reader.ReadSingle();
                            value = (x, y);
                            break;

                        case VariantType.Vector3:
                            float v3x = reader.ReadSingle();
                            float v3y = reader.ReadSingle();
                            float v3z = reader.ReadSingle();
                            value = (v3x, v3y, v3z);
                            break;

                        case VariantType.UnsignedInt:
                            value = reader.ReadUInt32();
                            break;

                        case VariantType.Int:
                            value = reader.ReadInt32();
                            break;

                        case VariantType.None:
                        default:
                            break;
                    }

                    list._variants.Add(new Variant(index, type, value));
                }

                return list;
            }
            catch
            {
                return null;
            }
        }

        public byte[] Encode()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((byte)_variants.Count);

            for (byte i = 0; i < _variants.Count; i++)
            {
                var v = _variants[i];
                writer.Write(i);
                writer.Write((byte)v.Type);

                switch (v.Type)
                {
                    case VariantType.Float:
                        writer.Write(v.AsFloat());
                        break;

                    case VariantType.String:
                        byte[] strBytes = Encoding.UTF8.GetBytes(v.AsString());
                        writer.Write((uint)strBytes.Length);
                        writer.Write(strBytes);
                        break;

                    case VariantType.Vector2:
                        if (v.Value is ValueTuple<float, float> v2)
                        {
                            writer.Write(v2.Item1);
                            writer.Write(v2.Item2);
                        }
                        else
                        {
                            writer.Write(0f);
                            writer.Write(0f);
                        }
                        break;

                    case VariantType.Vector3:
                        if (v.Value is ValueTuple<float, float, float> v3)
                        {
                            writer.Write(v3.Item1);
                            writer.Write(v3.Item2);
                            writer.Write(v3.Item3);
                        }
                        else
                        {
                            writer.Write(0f);
                            writer.Write(0f);
                            writer.Write(0f);
                        }
                        break;

                    case VariantType.UnsignedInt:
                        writer.Write(v.AsUInt());
                        break;

                    case VariantType.Int:
                        writer.Write(v.AsInt());
                        break;

                    case VariantType.None:
                    default:
                        break;
                }
            }

            return ms.ToArray();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"VariantList({_variants.Count}): [");
            for (int i = 0; i < _variants.Count; i++)
            {
                var v = _variants[i];
                sb.Append($"{i}:({v.Type}){v.Value}");
                if (i < _variants.Count - 1)
                    sb.Append(", ");
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
