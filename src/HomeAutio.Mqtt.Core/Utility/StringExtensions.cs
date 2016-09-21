using System.Text.RegularExpressions;

namespace HomeAutio.Mqtt.Core.Utilities
{
    public static class StringExtensions
    {
        public static string Sluggify(this string value)
        {
            return Regex.Replace(value, @"[^a-zA-Z0-9-]", "");
        }
    }
}
