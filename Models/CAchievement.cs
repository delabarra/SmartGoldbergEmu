namespace SmartGoldbergEmu.Models
{
    // Achievement model for Goldberg achievements.json format
    public class CAchievement
    {
        public string name { get; set; }
        public string displayName { get; set; }
        public string description { get; set; }
        public int hidden { get; set; }
        public string icon { get; set; }
        public string icongray { get; set; }
        public string icon_gray { get; set; }
    }
}
