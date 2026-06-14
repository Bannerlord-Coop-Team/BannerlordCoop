using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PartyWageLimitHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyWageLimitHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PartyWageLimitHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<WagePaymentLimitSet>(Handle_WagePaymentLimitSet);
        messageBroker.Subscribe<SetWagePaymentLimit>(Handle_SetWagePaymentLimit);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WagePaymentLimitSet>(Handle_WagePaymentLimitSet);
        messageBroker.Unsubscribe<SetWagePaymentLimit>(Handle_SetWagePaymentLimit);
    }

    private void Handle_WagePaymentLimitSet(MessagePayload<WagePaymentLimitSet> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new SetWagePaymentLimit(mobilePartyId, obj.What.NewValue);
        network.SendAll(message);
    }

    private void Handle_SetWagePaymentLimit(MessagePayload<SetWagePaymentLimit> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

        mobileParty.SetWagePaymentLimit(obj.What.NewValue);
    }
}
