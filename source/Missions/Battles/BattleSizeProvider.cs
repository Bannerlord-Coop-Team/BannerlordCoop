using System;
using GameInterface.Configuration;

namespace Missions.Battles;

public interface IBattleSizeProvider
{
    int GetBattleSize();
}

public class BattleSizeProvider : IBattleSizeProvider
{
    internal const int DefaultBattleSize = 1000;
    internal const int MinimumBattleSize = 200;
    internal const int MaximumBattleSize = 1000;

    private readonly IModConfig modConfig;

    public BattleSizeProvider(IModConfig modConfig)
    {
        if (modConfig == null) throw new ArgumentNullException(nameof(modConfig));

        this.modConfig = modConfig;
    }

    public int GetBattleSize()
    {
        int configuredBattleSize = modConfig.Data?.ModOptions?.BattleSize ?? DefaultBattleSize;
        return Math.Max(MinimumBattleSize, Math.Min(MaximumBattleSize, configuredBattleSize));
    }
}
