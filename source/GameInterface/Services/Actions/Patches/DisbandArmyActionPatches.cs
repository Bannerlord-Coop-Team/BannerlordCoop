using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.SiegeEvents;
using HarmonyLib;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<DisbandArmyActionPatches>();

    [HarmonyPatch(typeof(DisbandArmyAction), nameof(DisbandArmyAction.ApplyInternal))]
    [HarmonyPrefix]
    private static bool ApplyInternalPrefix(
        Army army,
        Army.ArmyDispersionReason reason,
        out AiSiegeTerminalTransitionState __state)
    {
        __state = default;
        if (ModInformation.IsServer)
        {
            if (reason == Army.ArmyDispersionReason.FoodProblem)
            {
                var leader = army?.LeaderParty;
                var siegeEvent = leader?.SiegeEvent;
                if (leader != null && siegeEvent != null)
                {
                    __state = new AiSiegeTerminalTransitionState(leader, siegeEvent);
                }
            }

            return true;
        }

        if (reason == Army.ArmyDispersionReason.DismissalRequestedWithInfluence)
        {
            var message = new DisbandArmyApplyInternal(army, MobileParty.MainParty);
            MessageBroker.Instance.Publish(army, message);
        }
        return false;
    }

    [HarmonyPatch(typeof(DisbandArmyAction), nameof(DisbandArmyAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void ApplyInternalPostfix(
        Army.ArmyDispersionReason reason,
        AiSiegeTerminalTransitionState __state)
    {
        if (ModInformation.IsClient
            || reason != Army.ArmyDispersionReason.FoodProblem
            || !__state.IsValid
            || !ContainerProvider.TryResolve<IAiSiegeTerminalPolicy>(out var terminalPolicy))
        {
            return;
        }

        terminalPolicy.ResolveFoodProblem(__state);
    }
    
    public static void DisbandArmyApplyInternal(Army army, MobileParty clientParty)
    {
        // The kingdom screen disbands an army the party is not in, but never one in a battle.
        // A stale screen can still send it after the army's leader joined one.
        if (army.LeaderParty?.MapEvent != null)
        {
            Logger.Warning("Ignoring a disband of the army led by {LeaderPartyId} from party {PartyId} while it is in a battle", army.LeaderParty.StringId, clientParty.StringId);
            return;
        }

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
