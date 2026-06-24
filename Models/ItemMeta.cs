namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Item metadata result from Steam Inventory Service.
    /// </summary>
    public class ItemMeta
    {
        public bool Success { get; set; }
        public string Digest { get; set; }
        public long Modified { get; set; }
    }
}

