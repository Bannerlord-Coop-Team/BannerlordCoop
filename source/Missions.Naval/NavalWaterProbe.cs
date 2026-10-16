#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Missions.Naval;

/// <summary>DEBUG: the arguments of <c>coop.debug.naval.water_probe start</c>.</summary>
public sealed class NavalWaterProbeSettings
{
    internal const string Usage = "Usage: coop.debug.naval.water_probe start [grid=5] [spacing=40] [seconds=120] | stop | status";

    public int Grid { get; private set; } = 5;

    public float Spacing { get; private set; } = 40f;

    public float Seconds { get; private set; } = 120f;

    /// <summary>Parses the knobs after <c>start</c>; why they were refused, or null.</summary>
    public static string TryParse(IEnumerable<string> knobs, out NavalWaterProbeSettings settings)
    {
        settings = null;
        var parsed = new NavalWaterProbeSettings();
        foreach (var knob in knobs ?? Array.Empty<string>())
        {
            int separator = knob?.IndexOf('=') ?? -1;
            if (separator <= 0) return $"Expected key=value, got {knob}. {Usage}";

            string key = knob.Substring(0, separator).Trim().ToLowerInvariant();
            string value = knob.Substring(separator + 1);
            switch (key)
            {
                case "grid":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int grid) || grid < 1 || grid > 15)
                        return "grid must be a whole number from 1 to 15.";
                    parsed.Grid = grid;
                    break;
                case "spacing":
                    if (!TryParseFloat(value, 1f, 500f, out float spacing)) return "spacing must be a number of metres from 1 to 500.";
                    parsed.Spacing = spacing;
                    break;
                case "seconds":
                    if (!TryParseFloat(value, 1f, 1800f, out float seconds)) return "seconds must be a number from 1 to 1800.";
                    parsed.Seconds = seconds;
                    break;
                default:
                    return $"Unknown knob {key}. {Usage}";
            }
        }

        settings = parsed;
        return null;
    }

    private static bool TryParseFloat(string value, float min, float max, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !float.IsNaN(result) &&
        result >= min && result <= max;
}

/// <summary>DEBUG: one hull's pose in a probe line: heave (origin height), pitch and roll in degrees, and the water under it.</summary>
internal readonly struct NavalWaterProbeHull
{
    public NavalWaterProbeHull(string shipId, string role, string mode, float heave, float pitch, float roll, float water)
    {
        ShipId = shipId;
        Role = role;
        Mode = mode;
        Heave = heave;
        Pitch = pitch;
        Roll = roll;
        Water = water;
    }

    public string ShipId { get; }
    public string Role { get; }
    public string Mode { get; }
    public float Heave { get; }
    public float Pitch { get; }
    public float Roll { get; }
    public float Water { get; }
}
#endif
