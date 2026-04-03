using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit.
/// </summary>
internal class SettlementExitEnterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementExitEnterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SettlementExitEnterHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PartyEnterSettlement>(Handle);
        messageBroker.Subscribe<PartyLeaveSettlement>(Handle);
        messageBroker.Subscribe<StartSettlementEncounter>(Handle);
        messageBroker.Subscribe<EndSettlementEncounter>(Handle);
        // Execute the server-approved party encounter on the game thread.
        messageBroker.Subscribe<StartPartyEncounterCommand>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyEnterSettlement>(Handle);
        messageBroker.Unsubscribe<PartyLeaveSettlement>(Handle);
        messageBroker.Unsubscribe<StartSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounter>(Handle);
        // Party encounter command cleanup.
        messageBroker.Unsubscribe<StartPartyEncounterCommand>(Handle);
    }

    private void Handle(MessagePayload<PartyEnterSettlement> obj)
    {
        var payload = obj.What;

        if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("PartyId not found: {id}", payload.PartyId);
            return;
        }

        if (objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) == false)
        {
            Logger.Error("SettlementId not found: {id}", payload.SettlementId);
            return;
        }

        EnterSettlementActionPatches.OverrideApplyForParty(mobileParty, settlement);
    }

    private void Handle(MessagePayload<PartyLeaveSettlement> obj)
    {
        var payload = obj.What;

        if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("PartyId not found: {id}", payload.PartyId);
            return;
        }

        LeaveSettlementActionPatches.OverrideApplyForParty(mobileParty);
    }

    private void Handle(MessagePayload<StartSettlementEncounter> obj)
    {
        var payload = obj.What;

        if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("PartyId not found: {id}", payload.PartyId);
            return;
        }

        if (objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) == false)
        {
            Logger.Error("SettlementId not found: {id}", payload.SettlementId);
            return;
        }

        var settlementParty = settlement.Party;
        if (settlementParty == null)
        {
            Logger.Error("Settlement {settlementName} did not have a party value", settlement.Name);
            return;
        }

        if (PlayerEncounter.Current != null) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                PlayerEncounter.Start();
                PlayerEncounter.Current.Init(mobileParty.Party, settlementParty, settlement);
            }
        }, blocking: true);
    }

    private void Handle(MessagePayload<EndSettlementEncounter> obj)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                PlayerEncounter.Finish(true);
                Campaign.Current.SaveHandler.SignalAutoSave();
            }
        }, blocking: true);
    }

    // This is the final step in the client→server→client encounter flow: it takes the server-approved
    // encounter command and executes PlayerEncounter.Start() locally under AllowedThread so Harmony
    // patches do not block it. Without this the approved encounter was never applied to the game.
    private void Handle(MessagePayload<StartPartyEncounterCommand> obj)
    {
        var payload = obj.What;

        Logger.Debug(
            "StartPartyEncounterCommand received: attacker={attacker} defender={defender}",
            payload.AttackerPartyId, payload.DefenderPartyId);

        if (objectManager.TryGetObject(payload.AttackerPartyId, out MobileParty attackerParty) == false)
        {
            Logger.Error("AttackerPartyId not found: {id}", payload.AttackerPartyId);
            return;
        }

        if (objectManager.TryGetObject(payload.DefenderPartyId, out MobileParty defenderParty) == false)
        {
            Logger.Error("DefenderPartyId not found: {id}", payload.DefenderPartyId);
            return;
        }

        if (PlayerEncounter.Current != null)
        {
            Logger.Debug(
                "StartPartyEncounterCommand blocked: encounter already active (attacker={attacker} defender={defender})",
                payload.AttackerPartyId, payload.DefenderPartyId);
            return;
        }

        Logger.Debug(
            "Executing StartPartyEncounterCommand: attacker={attacker} defender={defender}",
            payload.AttackerPartyId, payload.DefenderPartyId);

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                PlayerEncounter.Start();
                PlayerEncounter.Current.Init(attackerParty.Party, defenderParty.Party, null);
            }
        }, blocking: true);
    }
}