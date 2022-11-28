using System.Text.RegularExpressions;

namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// String extensions.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Sluggifies a string by removing non-alphanumeric chars.
        /// </summary>
        /// <param name="value">Value to sluggify.</param>
        /// <returns>Sluggified string.</returns>
        public static string Sluggify(this string value)
        {
            return SlugReplaceRegex().Replace(value, string.Empty);
        }

        /// <summary>
        /// Compiled regex for valid slug generation.
        /// </summary>
        /// <returns>A compiled <see cref="Regex"/>.</returns>
        [GeneratedRegex(@"[^a-zA-Z0-9-]")]
        private static partial Regex SlugReplaceRegex();
    }
}
