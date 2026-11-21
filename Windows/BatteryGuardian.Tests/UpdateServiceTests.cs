using System;
using Xunit;

namespace BatteryGuardian.Tests
{
    public class UpdateServiceTests
    {
        // ============================================================
        //  ParseTagToVersion — valid inputs
        // ============================================================

        [Fact]
        public void ParseTagToVersion_StandardSemver_ReturnsVersion()
        {
            Assert.Equal(new Version(1, 9, 0, 0), UpdateService.ParseTagToVersion("v1.9.0"));
        }

        [Fact]
        public void ParseTagToVersion_NoVPrefix_ReturnsVersion()
        {
            Assert.Equal(new Version(1, 9, 0, 0), UpdateService.ParseTagToVersion("1.9.0"));
        }

        [Fact]
        public void ParseTagToVersion_TwoComponents_PadsToZero()
        {
            Assert.Equal(new Version(1, 9, 0, 0), UpdateService.ParseTagToVersion("v1.9"));
        }

        [Fact]
        public void ParseTagToVersion_OneComponent_PadsToZero()
        {
            Assert.Equal(new Version(2, 0, 0, 0), UpdateService.ParseTagToVersion("v2"));
        }

        [Fact]
        public void ParseTagToVersion_UppercaseV_Works()
        {
            Assert.Equal(new Version(1, 9, 0, 0), UpdateService.ParseTagToVersion("V1.9.0"));
        }

        // ============================================================
        //  ParseTagToVersion — invalid inputs
        // ============================================================

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("v")]
        [InlineData("garbage")]
        [InlineData("release-notes")]
        public void ParseTagToVersion_InvalidInput_ReturnsNull(string? input)
        {
            Assert.Null(UpdateService.ParseTagToVersion(input!));
        }

        // ============================================================
        //  Version comparison logic (simulating what CheckForUpdateAsync does)
        // ============================================================

        [Fact]
        public void NewerRemoteVersion_IsDetectedAsUpdate()
        {
            var remote = UpdateService.ParseTagToVersion("v1.9.0")!;
            var current = new Version(1, 8, 0);

            Assert.True(remote > current);
        }

        [Fact]
        public void SameVersion_IsNotAnUpdate()
        {
            var remote = UpdateService.ParseTagToVersion("v1.9.0")!;
            var current = new Version(1, 9, 0, 0);

            Assert.False(remote > current);
        }

        [Fact]
        public void OlderRemoteVersion_IsNotAnUpdate()
        {
            var remote = UpdateService.ParseTagToVersion("v1.7.0")!;
            var current = new Version(1, 9, 0, 0);

            Assert.False(remote > current);
        }

        [Fact]
        public void MajorVersionBump_IsDetected()
        {
            var remote = UpdateService.ParseTagToVersion("v2.0.0")!;
            var current = new Version(1, 9, 0, 0);

            Assert.True(remote > current);
        }

        [Fact]
        public void PatchVersionBump_IsDetected()
        {
            var remote = UpdateService.ParseTagToVersion("v1.9.1")!;
            var current = new Version(1, 9, 0, 0);

            Assert.True(remote > current);
        }
    }
}