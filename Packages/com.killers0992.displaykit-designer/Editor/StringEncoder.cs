namespace DisplayKitDesigner
{
    public static class StringEncoder
    {
        /// <summary>
        /// Escapes all double quotes in the input string for valid C# string literals.
        /// </summary>
        /// <param name="input">The original string</param>
        /// <returns>The escaped string</returns>
        public static string EscapeQuotes(this string input)
        {
            if (input == null)
                return null;

            return input
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");
        }
    }
}
