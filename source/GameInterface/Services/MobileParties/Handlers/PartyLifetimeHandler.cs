using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Lifetime handler for parties
/// </summary>
internal class PartyLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public PartyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager) 
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<CreateParty>(Handle_CreateParty);
        messageBroker.Subscribe<DestroyParty>(Handle_DestroyParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CreateParty>(Handle_CreateParty);
        messageBroker.Unsubscribe<DestroyParty>(Handle_DestroyParty);
    }

    private void Handle_DestroyParty(MessagePayload<DestroyParty> payload)
    {
        var stringId = payload.What.Data.StringId;

        PartyLifetimePatches.OverrideRemoveParty(stringId);
    }

    private void Handle_CreateParty(MessagePayload<CreateParty> payload)
    {
        var stringId = payload.What.Data.StringId;

        PartyLifetimePatches.OverrideCreateNewParty(stringId);
    }
}
