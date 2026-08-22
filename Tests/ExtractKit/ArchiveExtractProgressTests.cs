using Xunit;
using SmartGoldbergEmu.ExtractKit;

namespace SmartGoldbergEmu.Tests.ExtractKit
{
    public sealed class ArchiveExtractProgressTests
    {
        [Fact]
        public void DecodeWeight_uses_entry_when_folder_is_not_larger()
        {
            Assert.Equal(20, ArchiveExtractProgress.DecodeWeight(20, 20, 89));
            Assert.Equal(20, ArchiveExtractProgress.DecodeWeight(20, 0, 89));
        }

        [Fact]
        public void DecodeWeight_maps_solid_folder_onto_remaining_extract_bytes()
        {
            Assert.Equal(89, ArchiveExtractProgress.DecodeWeight(20, 132, 89));
            Assert.Equal(50, ArchiveExtractProgress.DecodeWeight(20, 50, 89));
        }

        [Fact]
        public void CompletedDelta_keeps_entry_weight_on_cache_hit()
        {
            Assert.Equal(20, ArchiveExtractProgress.CompletedDelta(20, 0, 89));
            Assert.Equal(20, ArchiveExtractProgress.CompletedDelta(20, 20, 89));
        }

        [Fact]
        public void CompletedDelta_credits_solid_folder_once_without_exceeding_remaining()
        {
            Assert.Equal(89, ArchiveExtractProgress.CompletedDelta(20, 132, 89));
            Assert.Equal(21, ArchiveExtractProgress.CompletedDelta(20, 21, 89));
        }

        [Fact]
        public void ScaleBytes_is_proportional_and_capped()
        {
            Assert.Equal(0, ArchiveExtractProgress.ScaleBytes(100, 0, 200));
            Assert.Equal(50, ArchiveExtractProgress.ScaleBytes(100, 100, 200));
            Assert.Equal(100, ArchiveExtractProgress.ScaleBytes(100, 200, 200));
            Assert.Equal(100, ArchiveExtractProgress.ScaleBytes(100, 250, 200));
        }
    }
}
