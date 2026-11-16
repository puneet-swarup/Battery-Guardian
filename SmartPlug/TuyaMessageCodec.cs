using System;
using System.IO;
using System.Buffers.Binary;

namespace BatteryGuardian.SmartPlug
{
 public static class TuyaMessageCodec
 {
 public const int Prefix = 0x00006699;
 public const int HeaderSize = 4;
 public const int ReturnCodeSize = 4;
 public const int MaxPacketSize = 16 * 1024;
 public const int MaxPayloadSize = MaxPacketSize - 8 - ReturnCodeSize;

 public sealed record TuyaFrame(int Sequence, int Command, byte[] Payload)
 {
 public override string ToString()
 {
 return string.Format("TuyaFrame Seq={0} Cmd=0x{1:X8} PayloadLen={2}", Sequence, Command, Payload.Length);
 }
 }

 public static byte[] Encode(int sequence, int command, byte[] payload)
 {
 ArgumentNullException.ThrowIfNull(payload);
 if (payload.Length > MaxPayloadSize)
 throw new ArgumentException("Tuya payload too large (" + payload.Length + " > " + MaxPayloadSize + ").");
 if (sequence < 0)
 throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be non-negative.");
 int totalLength = 8 + payload.Length;
 byte[] buf = new byte[16 + payload.Length];
 BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0), Prefix);
 BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(4), totalLength);
 BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(8), sequence);
 BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(12), command);
 Buffer.BlockCopy(payload, 0, buf, 16, payload.Length);
 return buf;
 }

 public static TuyaFrame Decode(byte[] data)
 {
 ArgumentNullException.ThrowIfNull(data);
 if (data.Length < 16)
 throw new TuyaProtocolException("Frame is too short (" + data.Length + " bytes).");
 int prefix = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
 if (prefix != Prefix)
 throw new TuyaProtocolException("Invalid Tuya prefix.");
 int totalLength = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
 if (totalLength < 8)
 throw new TuyaProtocolException("Invalid Tuya packet length.");
 if (8 + totalLength != data.Length)
 throw new TuyaProtocolException("Frame length mismatch.");
 int sequence = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(8, 4));
 int command = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(12, 4));
 int payloadLength = data.Length - 16;
 byte[] payload = new byte[payloadLength];
 Buffer.BlockCopy(data, 16, payload, 0, payloadLength);
 return new TuyaFrame(sequence, command, payload);
 }

 public static bool TryReadFrame(Stream stream, out byte[] frame)
 {
 frame = Array.Empty<byte>();
 byte[] header = new byte[8];
 if (!ReadExactly(stream, header, 0)) return false;
 int prefix = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
 if (prefix != Prefix)
 throw new TuyaProtocolException("Invalid Tuya prefix.");
 int totalLength = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4));
 if (totalLength < 8 || totalLength > MaxPacketSize)
 throw new TuyaProtocolException("Invalid Tuya packet length.");
 byte[] buf = new byte[8 + totalLength];
 Buffer.BlockCopy(header, 0, buf, 0, 8);
 if (!ReadExactly(stream, buf, 8)) return false;
 frame = buf;
 return true;
 }

 private static bool ReadExactly(Stream stream, byte[] buf, int offset)
 {
 int remaining = buf.Length - offset;
 while (remaining > 0)
 {
 int read = stream.Read(buf, offset, remaining);
 if (read <= 0) return false;
 offset += read;
 remaining -= read;
 }
 return true;
 }
 }

 public class TuyaProtocolException : Exception
 {
 public TuyaProtocolException(string message) : base(message) { }
 public TuyaProtocolException(string message, Exception inner) : base(message, inner) { }
 }
}
