using System;
using System.Text;
using Glasscore.Simulation;

namespace Glasscore.Net
{
    /// <summary>Little-endian binary writer with a reusable, growable buffer (no per-message allocations).</summary>
    public sealed class NetWriter
    {
        private byte[] _data;
        public int Length { get; private set; }
        public byte[] Data => _data;

        public NetWriter(int capacity = 256) { _data = new byte[capacity]; }

        public NetWriter Reset() { Length = 0; return this; }

        private void Ensure(int extra)
        {
            if (Length + extra <= _data.Length) return;
            int size = _data.Length * 2;
            while (size < Length + extra) size *= 2;
            Array.Resize(ref _data, size);
        }

        public void Byte(byte v) { Ensure(1); _data[Length++] = v; }
        public void SByte(sbyte v) => Byte((byte)v);
        public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);

        public void UShort(ushort v)
        {
            Ensure(2);
            _data[Length++] = (byte)v;
            _data[Length++] = (byte)(v >> 8);
        }

        public void Short(short v) => UShort((ushort)v);

        public void UInt(uint v)
        {
            Ensure(4);
            _data[Length++] = (byte)v;
            _data[Length++] = (byte)(v >> 8);
            _data[Length++] = (byte)(v >> 16);
            _data[Length++] = (byte)(v >> 24);
        }

        public void Int(int v) => UInt((uint)v);
        public void Float(float v) => Int(BitConverter.SingleToInt32Bits(v));
        public void Double(double v) { long bits = BitConverter.DoubleToInt64Bits(v); UInt((uint)bits); UInt((uint)(bits >> 32)); }

        public void Vec3(Vec3 v) { Float(v.X); Float(v.Y); Float(v.Z); }

        public void String(string s, int maxBytes = 255)
        {
            s = s ?? string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            int n = Math.Min(bytes.Length, Math.Min(maxBytes, ushort.MaxValue));
            UShort((ushort)n);
            Bytes(bytes, 0, n);
        }

        public void Bytes(byte[] src, int offset, int count)
        {
            Ensure(count);
            Buffer.BlockCopy(src, offset, _data, Length, count);
            Length += count;
        }

        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Buffer.BlockCopy(_data, 0, copy, 0, Length);
            return copy;
        }
    }

    /// <summary>Bounds-checked reader. Malformed input throws <see cref="FormatException"/>.</summary>
    public struct NetReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        public int Position;

        public NetReader(byte[] data, int offset, int count)
        {
            _data = data;
            Position = offset;
            _end = offset + count;
        }

        public NetReader(byte[] data) : this(data, 0, data.Length) { }

        public int Remaining => _end - Position;

        private void Need(int n)
        {
            if (Position + n > _end) throw new FormatException("Packet truncated.");
        }

        public byte Byte() { Need(1); return _data[Position++]; }
        public sbyte SByte() => (sbyte)Byte();
        public bool Bool() => Byte() != 0;

        public ushort UShort()
        {
            Need(2);
            ushort v = (ushort)(_data[Position] | (_data[Position + 1] << 8));
            Position += 2;
            return v;
        }

        public short Short() => (short)UShort();

        public uint UInt()
        {
            Need(4);
            uint v = (uint)(_data[Position] | (_data[Position + 1] << 8) | (_data[Position + 2] << 16) | (_data[Position + 3] << 24));
            Position += 4;
            return v;
        }

        public int Int() => (int)UInt();
        public float Float() => BitConverter.Int32BitsToSingle(Int());
        public double Double() { uint lo = UInt(); uint hi = UInt(); return BitConverter.Int64BitsToDouble((long)((ulong)hi << 32 | lo)); }
        public Vec3 Vec3() => new Vec3(Float(), Float(), Float());

        public string String()
        {
            int n = UShort();
            Need(n);
            string s = Encoding.UTF8.GetString(_data, Position, n);
            Position += n;
            return s;
        }

        public byte[] Bytes(int count)
        {
            Need(count);
            var b = new byte[count];
            Buffer.BlockCopy(_data, Position, b, 0, count);
            Position += count;
            return b;
        }
    }
}
