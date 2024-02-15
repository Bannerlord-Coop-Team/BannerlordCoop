using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

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

        messageBroker.Subscribe<CampaignStateEntered>(Handle_CampaignStateEntered);

        messageBroker.Subscribe<CreateParty>(Handle_CreateParty);
        messageBroker.Subscribe<DestroyParty>(Handle_DestroyParty);
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

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignStateEntered>(Handle_CampaignStateEntered);;

        if (Campaign.Current == null)
            return;

        CampaignEvents.MobilePartyCreated.ClearListeners(this);
    }

    public void RegisterPartyListeners()
    {
        if (Campaign.Current == null)
        {
            Logger.Warning("Unable to register party life-cycle listeners, no active campaign");
            return;
        }

        //CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, Handle_MobilePartyCreated);
        //CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, Handle_MobilePartyDestroyed);
    }

    public void Handle_CampaignStateEntered(MessagePayload<CampaignStateEntered> obj)
    {
        RegisterPartyListeners();
    }
}
