using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches leaving settlement to remove any leaves from null settlement
/// </summary>

[HarmonyPatch(typeof(LeaveSettlementAction))]
public class LeaveSettlementActionPatches
{
    // The join-time hero switch runs vanilla's character-change settlement eject while the client
    // sync policy still allows originals (the client is in LoadingState), which would pop the
    // reloaded party outside on this client only while the server's save keeps it inside. Set
    // around ChangePlayerCharacterAction.Apply in HeroInterface.SwitchToPlayer; game-thread only.
    internal static bool SuppressForPlayerSwitch;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(LeaveSettlementAction.ApplyForParty))]
    private static bool Prefix(MobileParty mobileParty, out Settlement __state)
    {
        __state = null;
        if (mobileParty.CurrentSettlement == null) return false;
        if (SuppressForPlayerSwitch) return false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            var message = new PartyLeaveSettlementAttempted(mobileParty);
            MessageBroker.Instance.Publish(mobileParty, message);
            return false;
        }

        __state = mobileParty.CurrentSettlement;
        return true;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(nameof(LeaveSettlementAction.ApplyForParty))]
    private static Exception Finalizer(
        MobileParty mobileParty,
        Settlement __state,
        Exception __exception)
    {
        // Vanilla clears CurrentSettlement before callbacks that can still throw.
        if (__state != null &&
            mobileParty.CurrentSettlement == null)
        {
            var message = new PartyLeaveSettlementApplied(mobileParty);
            MessageBroker.Instance.Publish(mobileParty, message);
        }

        return __exception;
    }
}
