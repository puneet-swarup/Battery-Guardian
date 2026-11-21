using System;
using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
    public class TuyaSmartPlugTests
    {
        [Fact]
        public void RelayMappingIsTriState()
        {
            Assert.Equal(RelayState.On, TuyaSmartPlug.MapRelay(true));
            Assert.Equal(RelayState.Off, TuyaSmartPlug.MapRelay(false));
            Assert.Equal(RelayState.Unknown, TuyaSmartPlug.MapRelay(null));
        }

        [Fact]
        public void SixteenCharacterKeyIsAcceptedAsIs()
        {
            byte[] key = TuyaLocalKey.Parse("0123456789abcdef");

            Assert.Equal(16, key.Length);
            Assert.Equal((byte)'0', key[0]);
            Assert.Equal((byte)'f', key[15]);
        }

        [Fact]
        public void ThirtyTwoHexDigitsBecomeSixteenBytes()
        {
            byte[] key = TuyaLocalKey.Parse("000102030405060708090a0b0c0d0e0f");

            Assert.Equal(16, key.Length);
            Assert.Equal(0x00, key[0]);
            Assert.Equal(0x0f, key[15]);
        }

        [Fact]
        public void ThirtyTwoHexDigitsAreNotMistakenForAStringKey()
        {
            // "0123456789abcdef" twice over looks like a 32-character key but is
            // valid hex, so it is read as 16 bytes. Worth pinning: the ambiguity
            // is inherent to the format, not a bug in the parser.
            byte[] key = TuyaLocalKey.Parse("0123456789abcdef0123456789abcdef");

            // Hex digits pair up: 01 23 45 ... ef 01 23 ... ef
            Assert.Equal(16, key.Length);
            Assert.Equal((byte)0x01, key[0]);
            Assert.Equal((byte)0x23, key[1]);
            Assert.Equal((byte)0xef, key[15]);
        }

        [Fact]
        public void KeyIsTrimmed()
        {
            Assert.Equal(16, TuyaLocalKey.Parse("  0123456789abcdef  ").Length);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("short")]
        [InlineData("0123456789abcdefghijklmnopqrstuv")] // 32 chars, but not hex: not a key either way
        public void InvalidKeysAreRejected(string key)
        {
            Assert.ThrowsAny<ArgumentException>(() => TuyaLocalKey.Parse(key));
        }
    }
}
