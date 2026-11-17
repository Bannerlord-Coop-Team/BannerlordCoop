using Common;
using Common.Messaging;
using Common.Logging;
using Common.Util;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Villages;

public interface IClientVillageHostileActionTracker : IGameAbstraction
{
    bool TryBeginRequest();
    void Complete();
    void ParkRaidContinuation(Settlement settlement);
    void CloseDeniedRaidContinuation();
}

internal sealed class ClientVillageHostileActionTracker : IClientVillageHostileActionTracker, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientVillageHostileActionTracker>();

    private readonly IMessageBroker messageBroker;
    private bool pending;
    private ParkedRaidContinuation parkedRaidContinuation;
    private PlayerEncounter continuationEncounter;

    public ClientVillageHostileActionTracker(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
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
        if (pending)
        {
            if (continuationEncounter != null)
                CloseDeniedRaidContinuation();
            return false;
        }
        pending = true;
        return true;
    }

    public void Complete()
    {
        pending = false;
        continuationEncounter = null;
    }

    public void CloseDeniedRaidContinuation()
    {
        pending = false;
        var encounter = continuationEncounter;
        continuationEncounter = null;
        if (encounter == null || !ReferenceEquals(encounter, PlayerEncounter.Current))
            return;

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
        if (ModInformation.IsServer || IsInMission()) return;

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
        if (encounter != null && parked.Encounter != null && !ReferenceEquals(encounter, parked.Encounter))
        {
            parkedRaidContinuation = null;
            Logger.Information("Dropping parked raid continuation for {SettlementId}: encounter replaced", settlement.StringId);
            return;
        }

        if (MobileParty.MainParty?.MapEvent != null ||
            (encounter != null && encounter.EncounterState != PlayerEncounterState.End))
            return;

        parkedRaidContinuation = null;
        PlayerEncounter.Start();
        PlayerEncounter.Current.SetupFields(PartyBase.MainParty, settlement.Party);
        continuationEncounter = PlayerEncounter.Current;
        messageBroker.Publish(this, new VillageHostileActionAttempted(VillageHostileAction.Raid, MobileParty.MainParty, settlement));
    }

    private static bool IsInMission()
    {
        return MissionState.Current != null || Mission.Current != null;
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
