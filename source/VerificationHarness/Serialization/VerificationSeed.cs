using System.Globalization;

namespace VerificationHarness.Serialization;

internal static class VerificationSeed
{
    public const string Default = "0x00000000000006c1";

    public static string Normalize(string value, string commandName)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            string digits = value.Substring(2);
            if (digits.Length == 16 && digits.All(Uri.IsHexDigit))
            {
                return "0x" + digits.ToLowerInvariant();
            }
        }
        else if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong numericSeed))
        {
            return $"0x{numericSeed:x16}";
        }

        throw new ArgumentException(
            $"{commandName} seed must be a non-negative decimal integer or 0x followed by 16 hexadecimal characters.");
    }
}
