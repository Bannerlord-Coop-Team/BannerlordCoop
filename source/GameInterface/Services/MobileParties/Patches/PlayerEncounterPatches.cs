using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for player encounters
/// </summary>

[HarmonyPatch(typeof(EncounterManager))]
internal class EncounterManagerPatches
{
    private static ILogger Logger = LogManager.GetLogger<EncounterManagerPatches>();

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.StartSettlementEncounter))]
    private static bool Prefix(MobileParty attackerParty, Settlement settlement)
    {
        if (ModInformation.IsServer) return true;

        if (attackerParty.IsPartyControlled() == false) return false;

        var message = new StartSettlementEncounterAttempted(
            attackerParty.StringId,
            settlement.StringId);
        MessageBroker.Instance.Publish(attackerParty, message);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.HandleEncounters))]
    internal static bool HandleEncountersPatch(ref float dt)
    {
        if (Campaign.Current.TimeControlMode != CampaignTimeControlMode.Stop)
        {
            for (int i = 0; i < Campaign.Current.MobileParties.Count; i++)
            {
                EncounterManager.HandleEncounterForMobileParty(Campaign.Current.MobileParties[i], dt);
            }
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.HandleEncounterForMobileParty))]
    internal static bool HandleEncounterForMobilePartyPatch(ref MobileParty mobileParty, ref float dt)
    {
        if (!mobileParty.IsActive || mobileParty.AttachedTo != null || mobileParty.MapEventSide != null || (mobileParty.CurrentSettlement != null && !mobileParty.IsGarrison) || (mobileParty.BesiegedSettlement != null && mobileParty.ShortTermBehavior != AiBehavior.AssaultSettlement) || (!mobileParty.IsCurrentlyEngagingParty && !mobileParty.IsCurrentlyEngagingSettlement && (mobileParty.Ai.AiBehaviorInteractable == null || mobileParty.ShortTermBehavior != AiBehavior.GoToPoint || mobileParty.Ai.AiBehaviorInteractable is PartyBase { IsSettlement: not false } || mobileParty.Ai.AiBehaviorInteractable is PartyBase { IsMobile: not false } || (mobileParty.Party == PartyBase.MainParty && PlayerEncounter.Current != null))))
        {
            return false;
        }
        if (PlayerEncounter.EncounteredMobileParty == mobileParty)
        {
            PlayerEncounter current = PlayerEncounter.Current;
            if (current == null || current.PlayerSide != BattleSideEnum.Defender)
            {
                return false;
            }
        }

        if (mobileParty.IsCurrentlyEngagingParty && mobileParty.ShortTermTargetParty is null)
        {
            Logger.Error("Party {partyId}, {var} was null", mobileParty.StringId, nameof(mobileParty.ShortTermTargetParty));
            return false;
        }

        if (mobileParty.IsCurrentlyEngagingSettlement && mobileParty.ShortTermTargetSettlement is null)
        {
            Logger.Error("Party {partyId}, {var} was null", mobileParty.StringId, nameof(mobileParty.ShortTermTargetSettlement));
            return false;
        }

        if (
            (!mobileParty.IsCurrentlyEngagingSettlement || mobileParty.ShortTermTargetSettlement == null || mobileParty.ShortTermTargetSettlement != mobileParty.CurrentSettlement) && 
            (!mobileParty.IsCurrentlyEngagingParty || 
                (mobileParty.ShortTermTargetParty.IsActive && 
                    (mobileParty.ShortTermTargetParty.CurrentSettlement == null || 
                        (mobileParty.ShortTermTargetParty.MapEvent != null && 
                            (mobileParty.ShortTermTargetParty.MapEvent.GetLeaderParty(BattleSideEnum.Attacker).MapFaction == mobileParty.MapFaction || mobileParty.ShortTermTargetParty.MapEvent.GetLeaderParty(BattleSideEnum.Defender).MapFaction == mobileParty.MapFaction)))))
                            && mobileParty.Ai.AiBehaviorInteractable.CanPartyInteract(mobileParty, dt))
        {
            mobileParty.Ai.AiBehaviorInteractable.OnPartyInteraction(mobileParty);
        }

        return false;
    }
}
