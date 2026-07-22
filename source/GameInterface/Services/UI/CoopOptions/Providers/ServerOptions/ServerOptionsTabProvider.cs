using Common.Messaging;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.UI.CoopOptions.Providers.ServerOptions.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.ServerOptions;

/// <summary>Builds the dedicated server's battle-size options tab.</summary>
public class ServerOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "ServerOptionsTab";
    public const string TabName = "Battle Size";
    public const string SectionTitleText = "Battle Size";
    public const string SectionDescriptionText = "Maximum number of human agents shared by every player in a battle";

    private static readonly int[] BattleSizeValues =
    {
        ServerBattleSizeProvider.MinimumBattleSize,
        300,
        400,
        500,
        600,
        800,
        ServerBattleSizeProvider.MaximumBattleSize
    };

    public string Id => TabId;

    public CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker, Action<CoopOptionsTabVM> onSelect)
    {
        return new CoopOptionsTabVM(
            Id,
            TabName,
            new CoopOptionsSectionVM[]
            {
                new BattleSizeSection(GetBattleSizeOrDefault(options), messageBroker)
            },
            onSelect);
    }

    public static int GetBattleSizeOrDefault(CoopOptionsData options)
    {
        if (TryGetBattleSize(options, out var battleSize))
        {
            return battleSize;
        }

        return ServerBattleSizeProvider.DefaultBattleSize;
    }

    public static bool TryGetBattleSize(CoopOptionsData options, out int battleSize)
    {
        battleSize = default;

        if (options == null) return false;
        if (!options.TryGetSection<BattleSizeSectionOptions>(TabId, BattleSizeSection.SectionId, out var sectionOptions)) return false;

        return sectionOptions.TryGetBattleSize(out battleSize);
    }

    public static int GetBattleSizeForIndex(int index)
    {
        var clampedIndex = Math.Max(0, Math.Min(BattleSizeValues.Length - 1, index));
        return BattleSizeValues[clampedIndex];
    }

    public static int GetNearestBattleSizeIndex(int battleSize)
    {
        battleSize = ServerBattleSizeProvider.ClampBattleSize(battleSize);
        var nearestIndex = 0;
        var nearestDistance = Math.Abs(BattleSizeValues[0] - battleSize);

        for (var index = 1; index < BattleSizeValues.Length; index++)
        {
            var distance = Math.Abs(BattleSizeValues[index] - battleSize);
            if (distance >= nearestDistance) continue;

            nearestIndex = index;
            nearestDistance = distance;
        }

        return nearestIndex;
    }

    public static bool IsSupportedBattleSize(int battleSize)
    {
        return Array.IndexOf(BattleSizeValues, battleSize) >= 0;
    }

    public static int NormalizeBattleSize(int battleSize)
    {
        return GetBattleSizeForIndex(GetNearestBattleSizeIndex(battleSize));
    }

    public static int MaximumBattleSizeIndex => BattleSizeValues.Length - 1;
}
