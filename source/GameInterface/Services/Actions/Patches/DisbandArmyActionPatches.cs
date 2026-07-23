using Common;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Actions.Patches;

/// <summary>
/// Patch for DisbandArmyAction.ApplyInternal
/// Specifically when Army.ArmyDispersionReason is DismissalRequestedWithInfluence since,
/// this is the only way ApplyInternal ever gets called by a client.
/// Sends it to the server so that the server executes it.
/// All side-effects such as influence or relation in DisbandArmyApplyInternal are synced with AutoSync
/// </summary>
[HarmonyPatch]
internal class DisbandArmyActionPatches
{
    [HarmonyPatch(typeof(DisbandArmyAction), nameof(DisbandArmyAction.ApplyInternal))]
    [HarmonyPrefix]
    private static bool ApplyInternalPrefix(Army army, Army.ArmyDispersionReason reason)
    {
        if (ModInformation.IsServer) return true;
        if (reason == Army.ArmyDispersionReason.DismissalRequestedWithInfluence)
        {
            var message = new DisbandArmyApplyInternal(army, MobileParty.MainParty);
            MessageBroker.Instance.Publish(army, message);
        }
        return false;
    }
    
    public static void DisbandArmyApplyInternal(Army army, MobileParty clientParty)
    {
        DiplomacyModel diplomacyModel = Campaign.Current.Models.DiplomacyModel;
        ChangeClanInfluenceAction.Apply(clientParty.LeaderHero.Clan, (float)(-(float)diplomacyModel.GetInfluenceCostOfDisbandingArmy()));
        foreach (MobileParty mobileParty in army.Parties.ToList<MobileParty>())
        {
            if (mobileParty != clientParty && mobileParty.LeaderHero != null)
            {
                ChangeRelationAction.ApplyPlayerRelation(mobileParty.LeaderHero, diplomacyModel.GetRelationCostOfDisbandingArmy(mobileParty == mobileParty.Army.LeaderParty), true, true);
            }
        }
        army.DisperseInternal(Army.ArmyDispersionReason.DismissalRequestedWithInfluence);
    }
}
