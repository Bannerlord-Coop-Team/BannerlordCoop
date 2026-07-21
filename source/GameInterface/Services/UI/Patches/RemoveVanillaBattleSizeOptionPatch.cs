using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine.Options;
using TaleWorlds.MountAndBlade.Options;
using ManagedOptionsType = TaleWorlds.MountAndBlade.ManagedOptions.ManagedOptionsType;

namespace GameInterface.Services.UI.Patches;

/// <summary>Removes the local vanilla battle-size control because the server owns the setting.</summary>
[HarmonyPatch(typeof(OptionsProvider), nameof(OptionsProvider.GetPerformanceGameplayOptions))]
internal class RemoveVanillaBattleSizeOptionPatch
{
    [HarmonyPostfix]
    static void RemoveBattleSize(ref IEnumerable<IOptionData> __result)
    {
        __result = FilterBattleSizeOption(__result);
    }

    internal static IEnumerable<IOptionData> FilterBattleSizeOption(IEnumerable<IOptionData> options)
    {
        return options.Where(option =>
            !Equals(option.GetOptionType(), ManagedOptionsType.BattleSize));
    }
}
