using GameInterface.Services;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace GameInterface.Services.UI;

public interface IPlayerKillFeedColorService : IGameAbstraction
{
    Color GetColor(string controllerId);
    bool TryGetColor(string controllerId, out PlayerKillFeedColor color);
    void SetColor(string controllerId, PlayerKillFeedColor color);
    IReadOnlyDictionary<string, PlayerKillFeedColor> GetColors();
}

public class PlayerKillFeedColorService : IPlayerKillFeedColorService
{
    private readonly Dictionary<string, PlayerKillFeedColor> colorsByControllerId = new Dictionary<string, PlayerKillFeedColor>();

    public Color GetColor(string controllerId)
    {
        if (TryGetColor(controllerId, out var color))
        {
            return color.ToColor();
        }

        return PlayerColorAssigner.GetColor(controllerId);
    }

    public bool TryGetColor(string controllerId, out PlayerKillFeedColor color)
    {
        if (string.IsNullOrEmpty(controllerId))
        {
            color = default;
            return false;
        }

        lock (colorsByControllerId)
        {
            return colorsByControllerId.TryGetValue(controllerId, out color);
        }
    }

    public void SetColor(string controllerId, PlayerKillFeedColor color)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        lock (colorsByControllerId)
        {
            colorsByControllerId[controllerId] = color;
        }
    }

    public IReadOnlyDictionary<string, PlayerKillFeedColor> GetColors()
    {
        lock (colorsByControllerId)
        {
            return colorsByControllerId.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
