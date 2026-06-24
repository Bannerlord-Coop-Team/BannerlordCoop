using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(ArmyManagementVM), nameof(ArmyManagementVM.ExecuteDone))]
internal class ArmyManagementVMExecuteDonePatch
{
    [HarmonyPrefix]
    static bool Prefix(ArmyManagementVM __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (!ModInformation.IsClient) return true; 

        if (!__instance.CanAffordInfluenceCost) return false;
        // Only the player party is in the cart — treat as disband
        if (__instance.PartiesInCart.Count == 1 && __instance.PartiesInCart[0].IsMainHero)
        {
            __instance.ExecuteDisbandArmy();
            return false;
        }

        // Cohesion boost is skipped on client; server applies it via vanilla and syncs via DynamicSync

        if (__instance.PartiesInCart.Count > 1 && MobileParty.MainParty.MapFaction.IsKingdomFaction)
        {
            var parties = __instance.PartiesInCart
                .Where(p => p.Party != MobileParty.MainParty)
                .Select(p => p.Party)
                .ToList();

            if (MobileParty.MainParty.Army == null)
            {
                // No existing army, request creation from server
                MessageBroker.Instance.Publish(__instance, new PlayerCreatedArmy(
                    (Kingdom)MobileParty.MainParty.MapFaction,
                    Hero.MainHero,
                    Hero.MainHero.HomeSettlement,
                    Army.ArmyTypes.Defender,
                    parties));
            }
            else
            {
                // Army already exists, request additional parties to be added
                MessageBroker.Instance.Publish(__instance, new PlayerAddedPartiesToArmy(
                    MobileParty.MainParty.Army,
                    parties));
            }
            // Deduct influence locally
            ChangeClanInfluenceAction.Apply(Clan.PlayerClan, (float)(-(float)(__instance.TotalCost - __instance._influenceSpentForCohesionBoosting)));
        }

        if (__instance._partiesToRemove.Count > 0)
        {
            // Request removal of dismissed parties from the army
            var removeIds = __instance._partiesToRemove.Select(p => p.Party).ToList();

            MessageBroker.Instance.Publish(__instance, new PlayerRemovedPartiesFromArmy(removeIds));

            __instance._partiesToRemove.Clear();
        }

        __instance._onClose();
        CampaignEventDispatcher.Instance.OnArmyOverlaySetDirty();

        return false;
    }
}
// refresh ui
//[HarmonyPatch(typeof(ArmyMenuOverlayVM), nameof(ArmyMenuOverlayVM.Refresh))]
//public class ArmyMenuOverlayVMRefreshPatch
//{
//    [HarmonyPrefix]
//    static bool Prefix(ArmyMenuOverlayVM __instance)
//    {
//        if (!ModInformation.IsClient) return true;

//        if (__instance.ArmyToUse == null)
//        {
//            __instance.PartyList.Clear();
//            return false;
//        }

//        return true;
//    }
//}