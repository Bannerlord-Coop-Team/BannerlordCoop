using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyEnterSettlement>(Handle);
        messageBroker.Unsubscribe<PartyLeaveSettlement>(Handle);
        messageBroker.Unsubscribe<StartSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounter>(Handle);
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
        });
    }

    private void Handle(MessagePayload<EndSettlementEncounter> obj)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                var mainParty = MobileParty.MainParty;
                var settlement = mainParty.CurrentSettlement ?? PlayerEncounter.EncounterSettlement;

                // Move the party out to the settlement gate
                if (settlement != null)
                    mainParty.Position = settlement.GatePosition;

                // Remove the party from the settlement explicitly. On the client
                // PlayerEncounter.Current is frequently already null at this point, which
                // makes Finish() a no-op that never reaches its LeaveSettlement() branch,
                // leaving the party registered inside the settlement.
                if (mainParty.CurrentSettlement != null)
                    LeaveSettlementAction.ApplyForParty(mainParty);

                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Finish(true);
                }
                else if (Campaign.Current.CurrentMenuContext != null)
                {
                    // No active encounter but a settlement menu is still open, this closes it
                    GameMenu.ExitToLast();
                }

                // Hold so the party does not immediately re-path back into the settlement.
                mainParty.SetMoveModeHold();

                Campaign.Current.SaveHandler.SignalAutoSave();
            }
        });
    }
}