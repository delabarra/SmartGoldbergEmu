using System.Globalization;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper methods for parsing and formatting INI file values.
    /// Provides consistent conversion between C# types and INI string representations.
    /// </summary>
    public static class IniParseHelper
    {
        /// <summary>
        /// Converts a boolean value to an integer string (0 or 1) for INI files.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <returns>"1" for true, "0" for false.</returns>
        public static string BoolToString(bool value)
        {
            return value ? "1" : "0";
        }

        /// <summary>
        /// Converts an integer string to a boolean value from INI files.
        /// </summary>
        /// <param name="value">The string value to parse ("1" or "0").</param>
        /// <returns>True if value is "1", false otherwise.</returns>
        public static bool StringToBool(string value)
        {
            return value == "1";
        }

        /// <summary>
        /// Parses a float value from an INI file string using invariant culture.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <returns>The parsed float value, or 0.0f if parsing fails.</returns>
        public static float ParseFloat(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return 0.0f;
        }

        /// <summary>
        /// Converts a float value to a string for INI files using invariant culture.
        /// </summary>
        /// <param name="value">The float value to convert.</param>
        /// <returns>The string representation of the float.</returns>
        public static string FloatToString(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses an integer value from an INI file string.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <returns>The parsed integer value, or 0 if parsing fails.</returns>
        public static int ParseInt(string value)
        {
            if (int.TryParse(value, out int result))
                return result;
            return 0;
        }

        /// <summary>
        /// Converts an integer value to a string for INI files.
        /// </summary>
        /// <param name="value">The integer value to convert.</param>
        /// <returns>The string representation of the integer.</returns>
        public static string IntToString(int value)
        {
            return value.ToString();
        }
    }
}

