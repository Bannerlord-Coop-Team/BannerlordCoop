using Common;
using Common.Messaging;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents;

internal static class RaidAiInterventionSuppression
{
    public static bool ShouldSuppressParty(MapEventSide side, PartyBase party)
    {
        var mobileParty = party?.MobileParty;
        if (!IsSuppressibleAiParty(mobileParty))
            return false;

        return side?.MapEvent?.IsRaidAiInterventionSuppressed() == true;
    }

    public static bool ShouldSuppressEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        return TrySuppressPartyAgainstTarget(attackerParty?.MobileParty, defenderParty) ||
               TrySuppressPartyAgainstTarget(defenderParty?.MobileParty, attackerParty);
    }

    public static bool ShouldSuppressSettlementEncounter(MobileParty party, Settlement settlement)
    {
        if (!IsSuppressibleAiParty(party))
            return false;

        if (!IsSuppressedRaidTarget(settlement))
            return false;

        HoldParty(party);
        return true;
    }

    public static bool ShouldSuppressMobilePartyEncounter(MobileParty party)
    {
        if (!IsSuppressibleAiParty(party))
            return false;

        if (IsSuppressedRaidTarget(party.ShortTermTargetParty?.Party) ||
            IsSuppressedRaidTarget(party.ShortTermTargetSettlement))
        {
            HoldParty(party);
            return true;
        }

        var interactable = party.Ai?.AiBehaviorInteractable;
        if (interactable is PartyBase targetParty && IsSuppressedRaidTarget(targetParty))
        {
            HoldParty(party);
            return true;
        }

        return false;
    }

    public static void SuppressJoinedDefenders(MapEvent mapEvent)
    {
        if (!mapEvent.IsRaidAiInterventionSuppressed())
            return;

        var defenderSide = mapEvent.DefenderSide;
        if (defenderSide == null)
            return;

        for (int i = defenderSide._battleParties.Count - 1; i >= 0; i--)
        {
            var mapEventParty = defenderSide._battleParties[i];
            var party = mapEventParty?.Party;
            if (!ShouldSuppressParty(defenderSide, party))
                continue;

            defenderSide._battleParties.RemoveAt(i);
            defenderSide._mapEvent.RemoveInvolvedPartyInternal(mapEventParty);

            if (party._mapEventSide == defenderSide)
                party._mapEventSide = null;

            MessageBroker.Instance.Publish(defenderSide, new MapEventPartyRemoved(defenderSide, mapEventParty));
            HoldParty(party.MobileParty);
        }
    }

    public static void BlockJoin(MapEventSide side, PartyBase party)
    {
        if (party?._mapEventSide == side)
            party._mapEventSide = null;

        HoldParty(party?.MobileParty);
    }

    private static bool TrySuppressPartyAgainstTarget(MobileParty party, PartyBase target)
    {
        if (!IsSuppressibleAiParty(party))
            return false;

        if (!IsSuppressedRaidTarget(target))
            return false;

        HoldParty(party);
        return true;
    }

    private static bool IsSuppressibleAiParty(MobileParty party)
    {
        return !MapEventConfig.AllowRaidAiIntervention &&
               party != null &&
               !party.IsPlayerParty();
    }

    private static bool IsSuppressedRaidTarget(PartyBase party)
    {
        if (party == null)
            return false;

        if (party.MapEvent?.IsRaidAiInterventionSuppressed() == true)
            return true;

        if (party.MobileParty?.MapEvent?.IsRaidAiInterventionSuppressed() == true)
            return true;

        return IsSuppressedRaidTarget(party.Settlement);
    }

    private static bool IsSuppressedRaidTarget(Settlement settlement)
    {
        return settlement?.Party?.MapEvent?.IsRaidAiInterventionSuppressed() == true;
    }

    private static void HoldParty(MobileParty party)
    {
        if (ModInformation.IsClient)
            return;

        if (party?.Ai == null)
            return;

        party.SetMoveModeHold();
        MessageBroker.Instance.Publish(party.Ai, new PartyBehaviorChangeAttempted(party.Ai, AiBehavior.Hold, null, party.Position));
    }
}