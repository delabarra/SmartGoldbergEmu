using AppDataKit;

namespace SmartGoldbergEmu.Models
{
    public sealed class ItemGeneratorResult
    {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public int ItemCount { get; private set; }

        // Section items.json was written from, so callers can patch the catalog without fetching again.
        public ItemsSection Section { get; private set; }

        public static ItemGeneratorResult Ok(int count, ItemsSection section)
        {
            return new ItemGeneratorResult { Success = true, ItemCount = count, Section = section };
        }

        public static ItemGeneratorResult Fail(string message)
        {
            return new ItemGeneratorResult { Success = false, ErrorMessage = message ?? "Unknown error." };
        }
    }
}
