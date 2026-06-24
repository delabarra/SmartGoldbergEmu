using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Models
{
    public sealed class GameAddCollectResult
    {
        public bool Cancelled { get; set; }
        public bool MetadataFetchFailed { get; set; }
        public GameAddBundle Bundle { get; set; }
    }
}
