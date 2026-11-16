using System;
using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 public class TuyaMessageCodecTests
 {
 [Fact]
 public void EncodeDecodeRoundTrip()
 {
 byte[] payload = { 1, 2, 3, 4, 5 };
 byte[] frame = TuyaMessageCodec.Encode(7, 8, payload);
 var decoded = TuyaMessageCodec.Decode(frame);
 Assert.Equal(7, decoded.Sequence);
 Assert.Equal(8, decoded.Command);
 Assert.Equal(payload, decoded.Payload);
 }

 [Fact]
 public void EncodeUses6699Prefix()
 {
 byte[] frame = TuyaMessageCodec.Encode(1, 7, Array.Empty<byte>());
 Assert.Equal(0x00, frame[0]);
 Assert.Equal(0x00, frame[1]);
 Assert.Equal(0x66, frame[2]);
 Assert.Equal(0x99, frame[3]);
 }

 [Fact]
 public void DecodeRejectsBadPrefix()
 {
 byte[] bad = { 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0, 7 };
 Assert.Throws<TuyaProtocolException>(() => TuyaMessageCodec.Decode(bad));
 }

 [Fact]
 public void DecodeRejectsShortFrame()
 {
 Assert.Throws<TuyaProtocolException>(() => TuyaMessageCodec.Decode(new byte[] { 0, 0, 0, 1 }));
 }

 [Fact]
 public void TryReadFrameHandlesFragmentedReads()
 {
 byte[] frame = TuyaMessageCodec.Encode(3, 8, new byte[] { 9, 9 });
 using var ms = new System.IO.MemoryStream(frame);
 Assert.True(TuyaMessageCodec.TryReadFrame(ms, out var read));
 Assert.Equal(frame, read);
 Assert.False(TuyaMessageCodec.TryReadFrame(ms, out _));
 }

 [Fact]
 public void TryReadFrameHandlesMultipleFrames()
 {
 byte[] f1 = TuyaMessageCodec.Encode(1, 7, new byte[] { 1 });
 byte[] f2 = TuyaMessageCodec.Encode(2, 8, new byte[] { 2 });
 byte[] combined = new byte[f1.Length + f2.Length];
 Buffer.BlockCopy(f1, 0, combined, 0, f1.Length);
 Buffer.BlockCopy(f2, 0, combined, f1.Length, f2.Length);
 using var ms = new System.IO.MemoryStream(combined);
 Assert.True(TuyaMessageCodec.TryReadFrame(ms, out var r1));
 Assert.True(TuyaMessageCodec.TryReadFrame(ms, out var r2));
 Assert.Equal(f1, r1);
 Assert.Equal(f2, r2);
 }
 }
}
