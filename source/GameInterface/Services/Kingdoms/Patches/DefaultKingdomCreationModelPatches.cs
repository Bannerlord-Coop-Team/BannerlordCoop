using GameInterface.Configuration;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(DefaultKingdomCreationModel))]
internal class DefaultKingdomCreationModelPatches
{
    [HarmonyPatch(nameof(DefaultKingdomCreationModel.MinimumClanTierToCreateKingdom), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool MinimumClanTierToCreateKingdomPrefix(ref int __result)
    {
        __result = ModConfigProvider.ModOptions.PlayerKingdomClanTierRequired;
        return false;
    }
}
