namespace SmartGoldbergEmu.Models
{
    // One entry in Goldberg achievements.json; property names match the file keys.
    public class CAchievement
    {
        public string name { get; set; }
        public string displayName { get; set; }
        public string description { get; set; }
        public int hidden { get; set; }
        public string icon { get; set; }
        public string icongray { get; set; }
        public string icon_gray { get; set; }
        // gbe_fork overlay shows this global unlock rate (0-100); omitted from JSON when null.
        public double? unlock_percentage { get; set; }
    }
}
