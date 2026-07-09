using Common.Logging;
using GameInterface.Services.Locations;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Interfaces;

/// <summary>
/// Applies settlement entry/exit and player-encounter changes to the game. Callers are responsible
/// for marshalling onto the game thread (and, on the client, for the <c>AllowedThread</c> scope that
/// lets the patched action run as a replicated change). The server must NOT use an allowed thread so
/// the downstream changes the action triggers (recruitment, etc.) replicate normally.
/// </summary>
public interface ISettlementInterface : IGameAbstraction
{
    /// <summary>
    /// Applies a party entering a settlement.
    /// </summary>
    void PartyEnterSettlement(MobileParty party, Settlement settlement);

    /// <summary>
    /// Applies a party leaving a settlement.
    /// </summary>
    void PartyLeaveSettlement(MobileParty party);

    /// <summary>
    /// Starts the local player's encounter with a settlement.
    /// </summary>
    void StartSettlementEncounter(MobileParty party, Settlement settlement);

    /// <summary>
    /// Ends the local player's settlement encounter.
    /// </summary>
    void EndSettlementEncounter();

    /// <summary>
    /// Server-only: updates a settlement's location population (hero/companion placement) after a
    /// party has entered it. No-op off the server.
    /// </summary>
    void OnPartyEnteredSettlement(Settlement settlement, MobileParty party);

    /// <summary>
    /// Server-only: updates a settlement's location population after a party has left it.
    /// </summary>
    void OnPartyLeftSettlement(MobileParty party);
}

internal class SettlementInterface : ISettlementInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementInterface>();

    private readonly SettlementPopulationTracker populationTracker;

    public SettlementInterface(SettlementPopulationTracker populationTracker)
    {
        this.populationTracker = populationTracker;
    }

    public void PartyEnterSettlement(MobileParty party, Settlement settlement)
    {
        EnterSettlementAction.ApplyForParty(party, settlement);
    }

    public void PartyLeaveSettlement(MobileParty party)
    {
        if (party.CurrentSettlement == null) return;

        LeaveSettlementAction.ApplyForParty(party);
    }

    public void StartSettlementEncounter(MobileParty party, Settlement settlement)
    {
        var settlementParty = settlement.Party;
        if (settlementParty == null)
        {
            // Settlement.Name dereferences Party, which is exactly what is null here - log the id instead.
            Logger.Error("Settlement {settlementId} did not have a party value", settlement.StringId);
            return;
        }

        if (PlayerEncounter.Current != null) return;

        PlayerEncounter.Start();
        PlayerEncounter.Current.Init(party.Party, settlementParty, settlement);
    }

    public void EndSettlementEncounter()
    {
        var mainParty = MobileParty.MainParty;

        // Mirror the native leave consequence (game_menu_settlement_leave_on_consequence). The party must
        // be moved to the settlement gate (outside) BEFORE finishing — LeaveSettlementAction only clears
        // CurrentSettlement, it does not reposition. Without this the party is still on the settlement
        // when the encounter ends, so EncounterManager.HandleEncounterForMobileParty immediately re-fires
        // StartSettlementEncounter and the player is put right back in.
        if (mainParty.CurrentSettlement != null)
            mainParty.Position = mainParty.CurrentSettlement.GatePosition;

        try
        {
            PlayerEncounter.Finish(true);
        }
        finally
        {
            Campaign.Current.PlayerEncounter = null;
        }

        // Hold AFTER finishing: Finish -> LeaveSettlementAction resets party behavior, which would
        // otherwise clobber the hold and let the party walk straight back into the settlement.
        mainParty.SetMoveModeHold();

        Campaign.Current.SaveHandler?.SignalAutoSave();
    }

    public void OnPartyEnteredSettlement(Settlement settlement, MobileParty party)
        => populationTracker.OnPartyEnteredSettlement(settlement, party);

    public void OnPartyLeftSettlement(MobileParty party)
        => populationTracker.OnPartyLeftSettlement(party);
}
