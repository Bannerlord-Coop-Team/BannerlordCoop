#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.SiegeEvents.Commands;
using GameInterface.Services.SiegeEvents.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

public sealed class Issue1837ContinuationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<Issue1837ContinuationHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;

    public Issue1837ContinuationHandler(IMessageBroker messageBroker, IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkIssue1837Continuation>(Handle);
    }

    public void Dispose() => messageBroker.Unsubscribe<NetworkIssue1837Continuation>(Handle);

    private void Handle(MessagePayload<NetworkIssue1837Continuation> payload)
    {
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (ModInformation.IsServer || request.ControllerId != controllerIdProvider.ControllerId ||
                request.SettlementId != "town_ES1" ||
                !objectManager.TryGetObject<Settlement>(request.SettlementId, out var settlement)) return;
            var party = MobileParty.MainParty;
            if (party == null || party.MapEvent != null || party.CurrentSettlement != null || party.Army != null) return;
            Logger.Information("[Issue1837Witness] continuation {Action} {Controller} {Settlement}",
                request.Action, request.ControllerId, settlement.StringId);
            if (request.Action == "enter" && party.BesiegerCamp == null && PlayerEncounter.Current == null)
                messageBroker.Publish(this, new StartSettlementEncounterAttempted(party, settlement));
            else if (request.Action == "assault" && party.BesiegerCamp?.LeaderParty == party &&
                party.BesiegedSettlement == settlement)
                messageBroker.Publish(this, new AssaultSiegeAttempted(party, settlement));
        });
    }
}
#endif
