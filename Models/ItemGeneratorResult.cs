namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Outcome of <see cref="SmartGoldbergEmu.Generators.ItemGenerator.GenerateAndSaveAsync"/>.
    /// </summary>
    public sealed class ItemGeneratorResult
    {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public int ItemCount { get; private set; }

        public static ItemGeneratorResult Ok(int count)
        {
            return new ItemGeneratorResult { Success = true, ItemCount = count };
        }

        public static ItemGeneratorResult Fail(string message)
        {
            return new ItemGeneratorResult { Success = false, ErrorMessage = message ?? "Unknown error." };
        }
    }
}
