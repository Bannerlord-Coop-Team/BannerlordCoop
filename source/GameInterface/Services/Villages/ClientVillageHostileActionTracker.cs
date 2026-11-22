using Common.Messaging;
using Common.Logging;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Villages;

public interface IClientVillageHostileActionTracker
{
    /// <summary>[Client, game thread] Begin tracking a hostile-action request if none is pending.</summary>
    bool TryBeginRequest();
    /// <summary>[Client, game thread] Clear the pending latch and continuation encounter.</summary>
    void Complete();
    /// <summary>[Client, game thread] End a denied request by releasing the latch before closing its continuation encounter.</summary>
    void CompleteDenied();
    /// <summary>[Client, game thread] Park a raid continuation until the current encounter ends.</summary>
    void ParkRaidContinuation(Settlement settlement);
}

// Tracks client hostile-action requests and raid continuations for one lifetime scope.
internal sealed class ClientVillageHostileActionTracker : IClientVillageHostileActionTracker, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientVillageHostileActionTracker>();

    private readonly IMessageBroker messageBroker;
    // A won raid's continuation, waiting for the current encounter to end.
    private ParkedRaidContinuation parkedRaidContinuation;
    // The encounter a continuation request opened, finished if the server denies it.
    private PlayerEncounter continuationEncounter;
    private bool pending;

    public ClientVillageHostileActionTracker(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        pending = false;
        parkedRaidContinuation = null;
        continuationEncounter = null;
    }

    public bool TryBeginRequest()
    {
        if (pending) return false;
        pending = true;
        return true;
    }

    public void Complete()
    {
        pending = false;
        continuationEncounter = null;
    }

    public void CompleteDenied()
    {
        var encounter = continuationEncounter;
        Complete();
        if (encounter == null || !ReferenceEquals(encounter, PlayerEncounter.Current))
            return;

        // Leave a live encounter or simulation alone when a denial races its start.
        if (encounter._mapEvent != null || encounter.BattleSimulation != null)
            return;

        encounter._stateHandled = true;
        PlayerEncounter.LeaveEncounter = true;
        PlayerEncounter.Finish(true);
    }

    public void ParkRaidContinuation(Settlement settlement)
    {
        parkedRaidContinuation = new ParkedRaidContinuation(settlement, PlayerEncounter.Current);
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (MissionState.Current != null || Mission.Current != null) return;
        // Wait for the pending reply, otherwise TryBeginRequest drops the continuation's request.
        if (pending) return;

        var parked = parkedRaidContinuation;
        if (parked == null) return;

        var settlement = parked.Settlement;
        if (PlayerCaptivity.IsCaptive)
        {
            parkedRaidContinuation = null;
            Logger.Information("Dropping parked raid continuation for {SettlementId}: captive", settlement.StringId);
            return;
        }

        var encounter = PlayerEncounter.Current;
        bool encounterReplaced = encounter != null && parked.Encounter != null && !ReferenceEquals(encounter, parked.Encounter);
        if (encounterReplaced)
        {
            parkedRaidContinuation = null;
            Logger.Information("Dropping parked raid continuation for {SettlementId}: encounter replaced", settlement.StringId);
            return;
        }

        bool encounterStillOpen = MobileParty.MainParty?.MapEvent != null ||
            (encounter != null && encounter.EncounterState != PlayerEncounterState.End);
        if (encounterStillOpen)
            return;

        parkedRaidContinuation = null;
        StartRaidContinuationEncounter(settlement);
    }

    private void StartRaidContinuationEncounter(Settlement settlement)
    {
        // The map-event teardown clears both parties before this continuation requests fresh approval.
        PlayerEncounter.Start();
        PlayerEncounter.Current.SetupFields(PartyBase.MainParty, settlement.Party);
        continuationEncounter = PlayerEncounter.Current;
        messageBroker.Publish(this, new VillageHostileActionAttempted(VillageHostileAction.Raid, MobileParty.MainParty, settlement));
    }

    private sealed class ParkedRaidContinuation
    {
        public ParkedRaidContinuation(Settlement settlement, PlayerEncounter encounter)
        {
            Settlement = settlement;
            Encounter = encounter;
        }

        public Settlement Settlement { get; }
        public PlayerEncounter Encounter { get; }
    }
}
