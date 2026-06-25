using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(ArmyManagementVM))]
internal class ArmyManagementVMPatch
{
    [HarmonyPatch(typeof(ArmyManagementVM), nameof(ArmyManagementVM.ExecuteDone))]
    [HarmonyPrefix]
    static bool Prefix(ArmyManagementVM __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true; 

        // Only the player party is in the cart — treat as disband
        if (__instance.PartiesInCart.Count == 1 && __instance.PartiesInCart[0].IsMainHero)
        {
            __instance.ExecuteDisbandArmy();

        }
        if (!__instance.CanAffordInfluenceCost) return false;
        // If the player clicked the +10 cohesion boost one or more times,
        // publish the total accumulated boost to be applied server-side.
        // BoostCohesionWithInfluence on each peer handles both cohesion and influence deduction,
        if (__instance.NewCohesion > __instance.Cohesion && MobileParty.MainParty.Army != null)
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
                foreach (var party in parties)
                {
                    if (party != MobileParty.MainParty)
                    {
                        MessageBroker.Instance.Publish(__instance, new MobilePartyInArmyAdded(
                        MobileParty.MainParty.Army,
                        party));
                        using (new AllowedThread())
                        {
                            ArmyPatches.AddMobilePartyInArmy(party, MobileParty.MainParty.Army);
                        }
                    }
                }
            }
        MessageBroker.Instance.Publish(__instance, new ChangeClanInfluence(__instance.TotalCost - __instance._influenceSpentForCohesionBoosting));
        }

        if (__instance._partiesToRemove.Count > 0)
        {
            bool flag = false;
            // Request removal of dismissed parties from the army
            var removeIds = __instance._partiesToRemove.Select(p => p.Party).ToList();
            foreach (var party in removeIds)
            {
                if (party == MobileParty.MainParty)
                { 
                    MessageBroker.Instance.Publish(__instance, new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, party));
                    ArmyPatches.RemoveMobilePartyInArmy(party, MobileParty.MainParty.Army);
                    flag = true;
                }
            }
            if (!flag)
            {
                foreach (var party2 in removeIds)
                {
                    Army army = MobileParty.MainParty.Army;
                    if (army != null && army.Parties.Contains(party2))
                    {
                        MessageBroker.Instance.Publish(__instance, new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, party2));
                        ArmyPatches.RemoveMobilePartyInArmy(party2, MobileParty.MainParty.Army);
                    }
                }
            }
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
        if (ModInformation.IsServer) return true;
        if ((GameMenuOverlay.MenuOverlayContextList)o != GameMenuOverlay.MenuOverlayContextList.ArmyDismiss) return true;

        var party = __instance._contextMenuItem?.Party?.MobileParty;
        if (party?.Army == null) return true;
        { 
            MessageBroker.Instance.Publish(__instance, new MobilePartyInArmyRemoved(party.Army, party));
            ArmyPatches.RemoveMobilePartyInArmy(party, MobileParty.MainParty.Army);
        }

        return false;
    }
}