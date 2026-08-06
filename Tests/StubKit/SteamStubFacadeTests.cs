using SmartGoldbergEmu.StubKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.StubKit
{
    public sealed class SteamStubFacadeTests
    {
        [Fact]
        public void Detect_null_or_empty_returns_none()
        {
            DetectResult fromNull = SteamStub.Detect(null);
            Assert.Equal(StubVariant.None, fromNull.Variant);
            Assert.False(fromNull.CanRemove);

            DetectResult fromEmpty = SteamStub.Detect(new byte[0]);
            Assert.Equal(StubVariant.None, fromEmpty.Variant);
            Assert.False(fromEmpty.CanRemove);
        }

        [Fact]
        public void Detect_invalid_pe_returns_none()
        {
            DetectResult result = SteamStub.Detect(new byte[] { 0x00, 0x01, 0x02, 0x03 });
            Assert.Equal(StubVariant.None, result.Variant);
            Assert.False(result.CanRemove);
        }

        [Fact]
        public void TryUnpack_null_or_empty_fails()
        {
            byte[] unpacked;
            StubUnpackInfo info;

            Assert.False(SteamStub.TryUnpack(null, UnpackOptions.Default, out unpacked, out info));
            Assert.Null(unpacked);
            Assert.False(string.IsNullOrWhiteSpace(info.ErrorMessage));

            Assert.False(SteamStub.TryUnpack(new byte[0], null, out unpacked, out info));
            Assert.Null(unpacked);
            Assert.False(string.IsNullOrWhiteSpace(info.ErrorMessage));
        }

        [Fact]
        public void TryUnpack_non_stub_mz_stub_fails()
        {
            // Minimal MZ header that is not a valid PE — Detect/TryUnpack should fail cleanly.
            var data = new byte[0x80];
            data[0] = (byte)'M';
            data[1] = (byte)'Z';

            byte[] unpacked;
            StubUnpackInfo info;
            Assert.False(SteamStub.TryUnpack(data, UnpackOptions.Default, out unpacked, out info));
            Assert.Null(unpacked);
            Assert.False(string.IsNullOrWhiteSpace(info.ErrorMessage));
        }

        [Fact]
        public void UnpackOptions_Default_clears_security_and_drops_bind()
        {
            UnpackOptions options = UnpackOptions.Default;
            Assert.False(options.KeepBind);
            Assert.True(options.ClearSecurity);
        }
    }
}
