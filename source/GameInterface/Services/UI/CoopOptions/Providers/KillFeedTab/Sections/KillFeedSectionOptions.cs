using GameInterface.Services.UI;
using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab.Sections;

public class KillFeedSectionOptions
{
    private static readonly PlayerKillFeedColor DefaultKillFeedColor = new PlayerKillFeedColor(59, 130, 246);

    [JsonPropertyName("killFeedColor")]
    public KillFeedColor KillFeedColor { get; set; }

    public static KillFeedSectionOptions FromColor(PlayerKillFeedColor color)
    {
        var options = new KillFeedSectionOptions();
        options.SetKillFeedColor(color);
        return options;
    }

    public PlayerKillFeedColor GetKillFeedColorOrDefault()
    {
        if (TryGetKillFeedColor(out var color))
        {
            return color;
        }

        return DefaultKillFeedColor;
    }

    public bool TryGetKillFeedColor(out PlayerKillFeedColor color)
    {
        if (KillFeedColor == null)
        {
            color = default;
            return false;
        }

        return PlayerKillFeedColor.TryCreate(KillFeedColor.Red, KillFeedColor.Green, KillFeedColor.Blue, out color);
    }

    public void SetKillFeedColor(PlayerKillFeedColor color)
    {
        KillFeedColor = new KillFeedColor
        {
            Red = color.Red,
            Green = color.Green,
            Blue = color.Blue
        };
    }
}

public class KillFeedColor
{
    public int Red { get; set; }
    public int Green { get; set; }
    public int Blue { get; set; }
}
