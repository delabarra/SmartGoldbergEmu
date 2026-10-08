namespace SmartGoldbergEmu.Models
{
    public class AppSearchResult
    {
        public ulong AppId { get; set; }

        public string Name { get; set; }

        // For example "PICS" or "Steam App List".
        public string Source { get; set; }

        public override string ToString()
        {
            return $"{Name} ({AppId})";
        }
    }
}

