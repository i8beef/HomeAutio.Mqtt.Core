using System.Text.RegularExpressions;

namespace HomeAutio.Mqtt.Core.Utilities
{
    /// <summary>
    /// String extensions.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Sluggifies a string by removing non-alphanumeric chars.
        /// </summary>
        /// <param name="value">Value to sluggify.</param>
        /// <returns>Sluggified string.</returns>
        public static string Sluggify(this string value)
        {
            return Regex.Replace(value, @"[^a-zA-Z0-9-]", string.Empty);
        }
    }
}
