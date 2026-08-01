using Common;
using GameInterface.Configuration;
using GameInterface.Extentions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.Companions.Patches.Disable;

[HarmonyPatch(typeof(CompanionsCampaignBehavior))]
internal class DisableCompanionsCampaignBehavior
{
    [HarmonyPatch(nameof(CompanionsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(CompanionsCampaignBehavior))]
internal class CompanionsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(CompanionsCampaignBehavior._desiredTotalCompanionCount), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool DesiredTotalCompanionCountGetterPrefix(ref float __result)
    {
        __result = ModConfigProvider.ModOptions.WandererLimit;
        return false;
    }

    /// <summary>
    /// Replace vanilla implementation to not use Hero.MainHero which is null on the headless server.
    /// </summary>
    [HarmonyPatch(nameof(CompanionsCampaignBehavior.TrySpawnNewCompanion))]
    [HarmonyPrefix]
    public static bool TrySpawnNewCompanionPrefix(CompanionsCampaignBehavior __instance)
    {
        if ((float)__instance._aliveCompanionTemplates.Count < __instance._desiredTotalCompanionCount)
        {
            Town targetTown = Town.AllTowns.GetRandomElementWithPredicate(delegate (Town x)
            {
                // Instead of checking if Hero.MainHero is in the settlement, check if any players are in the settlement
                bool playerInSettlement = false;
                foreach(var playerHero in Campaign.Current.CampaignObjectManager.GetPlayerHeroes())
                {
                    if (playerHero.CurrentSettlement == x.Settlement)
                    {
                        playerInSettlement = true;
                        break;
                    }
                }

                if (!playerInSettlement && x.Settlement.SiegeEvent == null)
                {
                    return x.Settlement.HeroesWithoutParty.AllQ(y => !y.IsWanderer || y.CompanionOf != null);
                }
                return false;
            });

            Settlement targetSettlement = targetTown?.Settlement;
            if (targetSettlement != null)
            {
                __instance.CreateCompanionAndAddToSettlement(targetSettlement);
            }
        }

        return false;
    }
}