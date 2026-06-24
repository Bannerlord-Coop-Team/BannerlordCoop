using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
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
        // If the player clicked the +10 cohesion boost one or more times,
        // publish the total accumulated boost to be applied server-side.
        // BoostCohesionWithInfluence on each peer handles both cohesion and influence deduction,
        if (__instance.NewCohesion > __instance.Cohesion)
        {
            int delta = __instance.NewCohesion - __instance.Cohesion;
            MessageBroker.Instance.Publish(__instance, new PlayerBoostedArmyCohesion(
                MobileParty.MainParty.Army.LeaderParty,
                (float)delta,
                __instance._influenceSpentForCohesionBoosting));
        }
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
            // Deduct influence for party recruitment only
            // cohesion boost cost is excluded
            // since it is handled by BoostCohesionWithInfluence on all peers.
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
[HarmonyPatch(typeof(GameMenuOverlay), "ExecuteTroopAction")]
public class GameMenuOverlayArmyDismissPatch
{
    [HarmonyPrefix]
    static bool Prefix(GameMenuOverlay __instance, object o)
    {
        if (!ModInformation.IsClient) return true;
        if ((GameMenuOverlay.MenuOverlayContextList)o != GameMenuOverlay.MenuOverlayContextList.ArmyDismiss) return true;

        var party = __instance._contextMenuItem?.Party?.MobileParty;
        if (party?.Army == null) return true;

        MessageBroker.Instance.Publish(__instance, new PlayerRemovedPartiesFromArmy(
            new List<MobileParty> { party }));

        return false;
    }
}