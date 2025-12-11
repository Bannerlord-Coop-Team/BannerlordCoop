using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using Serilog;

namespace GameInterface.Services.MobileParties.Handlers;

internal class SettlementAttemptFlowHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementAttemptFlowHandler>();

    private readonly IMessageBroker messageBroker;

    public SettlementAttemptFlowHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<PartyEnterSettlementAttempted>(Handle);
        messageBroker.Subscribe<PartyLeaveSettlementAttempted>(Handle);
        messageBroker.Subscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<EndSettlementEncounterAttempted>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyEnterSettlementAttempted>(Handle);
        messageBroker.Unsubscribe<PartyLeaveSettlementAttempted>(Handle);
        messageBroker.Unsubscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounterAttempted>(Handle);
    }

    private void Handle(MessagePayload<PartyEnterSettlementAttempted> obj)
    {
        Logger.Information("Forward PartyEnterSettlementAttempted -> PartyEnterSettlement party={partyId} settlement={settlementId}", obj.What.PartyId, obj.What.SettlementId);
        try
        {
            TaleWorlds.CampaignSystem.Party.MobileParty party = null;
            foreach (var p in TaleWorlds.CampaignSystem.Party.MobileParty.All)
            {
                if (p != null && p.StringId == obj.What.PartyId)
                {
                    party = p;
                    break;
                }
            }
            var settlement = TaleWorlds.CampaignSystem.Settlements.Settlement.Find(obj.What.SettlementId);
            if (party != null && settlement != null && party.CurrentSettlement == settlement)
            {
                Logger.Information("Déjà dans le settlement, skip forward party={partyId} settlement={settlementId}", obj.What.PartyId, obj.What.SettlementId);
                return;
            }
        }
        catch { }
        messageBroker.Publish(this, new PartyEnterSettlement(obj.What.SettlementId, obj.What.PartyId));
    }

    private void Handle(MessagePayload<PartyLeaveSettlementAttempted> obj)
    {
        Logger.Information("Forward PartyLeaveSettlementAttempted -> PartyLeaveSettlement party={partyId}", obj.What.PartyId);
        messageBroker.Publish(this, new PartyLeaveSettlement(obj.What.PartyId));
    }

    private void Handle(MessagePayload<StartSettlementEncounterAttempted> obj)
    {
        Logger.Information("Forward StartSettlementEncounterAttempted -> StartSettlementEncounter party={partyId} settlement={settlementId}", obj.What.PartyId, obj.What.SettlementId);
        messageBroker.Publish(this, new StartSettlementEncounter(obj.What.PartyId, obj.What.SettlementId));
    }

    private void Handle(MessagePayload<EndSettlementEncounterAttempted> obj)
    {
        Logger.Information("Forward EndSettlementEncounterAttempted -> EndSettlementEncounter party={partyId}", obj.What.PartyId);
        messageBroker.Publish(this, new EndSettlementEncounter());
    }
}
