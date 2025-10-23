using System;
using System.Text.Json;

namespace HealthFlow.Shared.Utils
{
    public static class JsonUtils
    {
        /// <summary>
        /// Determines whether a string is valid JSON and can be deserialized into the target type.
        /// </summary>
        public static bool IsJson<T>(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            try
            {
                var obj = JsonSerializer.Deserialize<T>(input);
                return obj != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Determines if the string looks like JSON (object or array syntax).
        /// </summary>
        public static bool LooksLikeJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var trimmed = input.Trim();
            return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                   (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
        }
    }
}
