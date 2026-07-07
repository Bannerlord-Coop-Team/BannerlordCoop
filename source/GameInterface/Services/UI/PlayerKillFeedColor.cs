using System;
using System.Globalization;
using TaleWorlds.Library;

namespace GameInterface.Services.UI;

public readonly struct PlayerKillFeedColor : IEquatable<PlayerKillFeedColor>
{
    public readonly int Red;
    public readonly int Green;
    public readonly int Blue;

    public PlayerKillFeedColor(int red, int green, int blue)
    {
        if (!IsValidComponent(red)) throw new ArgumentOutOfRangeException(nameof(red));
        if (!IsValidComponent(green)) throw new ArgumentOutOfRangeException(nameof(green));
        if (!IsValidComponent(blue)) throw new ArgumentOutOfRangeException(nameof(blue));

        Red = red;
        Green = green;
        Blue = blue;
    }

    public static bool TryCreate(int red, int green, int blue, out PlayerKillFeedColor color)
    {
        if (!IsValidComponent(red) || !IsValidComponent(green) || !IsValidComponent(blue))
        {
            color = default;
            return false;
        }

        color = new PlayerKillFeedColor(red, green, blue);
        return true;
    }

    public static PlayerKillFeedColor Clamp(int red, int green, int blue)
    {
        return new PlayerKillFeedColor(
            ClampComponent(red),
            ClampComponent(green),
            ClampComponent(blue));
    }

    public static bool TryParseHex(string hex, out PlayerKillFeedColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(hex)) return false;

        string value = hex.Trim();
        if (value.StartsWith("#", StringComparison.Ordinal))
        {
            value = value.Substring(1);
        }

        // Bannerlord UI color strings carry alpha; player input may too. The kill-feed color is always opaque.
        if (value.Length == 8)
        {
            value = value.Substring(0, 6);
        }

        if (value.Length != 6) return false;

        if (!int.TryParse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red))
            return false;
        if (!int.TryParse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green))
            return false;
        if (!int.TryParse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            return false;

        return TryCreate(red, green, blue, out color);
    }

    public Color ToColor() => new Color(Red / 255f, Green / 255f, Blue / 255f);

    public string ToHex() => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public string ToColorString() => $"#{Red:X2}{Green:X2}{Blue:X2}FF";

    public bool Equals(PlayerKillFeedColor other) =>
        Red == other.Red &&
        Green == other.Green &&
        Blue == other.Blue;

    public override bool Equals(object obj) => obj is PlayerKillFeedColor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Red;
            hashCode = (hashCode * 397) ^ Green;
            hashCode = (hashCode * 397) ^ Blue;
            return hashCode;
        }
    }

    private static bool IsValidComponent(int component) => component >= 0 && component <= 255;

    private static int ClampComponent(int component)
    {
        if (component < 0) return 0;
        if (component > 255) return 255;
        return component;
    }
}
