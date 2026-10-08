using System.Globalization;

namespace SmartGoldbergEmu.Helpers
{
    public static class IniParseHelper
    {
        public static string BoolToString(bool value)
        {
            return value ? "1" : "0";
        }

        public static bool StringToBool(string value)
        {
            return value == "1";
        }

        public static float ParseFloat(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return 0.0f;
        }

        public static string FloatToString(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static int ParseInt(string value)
        {
            if (int.TryParse(value, out int result))
                return result;
            return 0;
        }

        public static string IntToString(int value)
        {
            return value.ToString();
        }
    }
}
