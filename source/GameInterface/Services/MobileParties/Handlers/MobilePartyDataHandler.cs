using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handler for party related messages.
/// </summary>
internal class MobilePartyDataHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyDataHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public MobilePartyDataHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<PartyComponentChanged>(Handle_PartyComponentChanged);
        messageBroker.Subscribe<NetworkChangePartyComponent>(Handle_ChangePartyComponent);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyComponentChanged>(Handle_PartyComponentChanged);
        messageBroker.Unsubscribe<NetworkChangePartyComponent>(Handle_ChangePartyComponent);
    }

    private void Handle_PartyComponentChanged(MessagePayload<PartyComponentChanged> payload)
    {
        var message = new NetworkChangePartyComponent(
            payload.What.PartyId,
            payload.What.ComponentId
        );

        network.SendAll(message);
    }

    private void Handle_ChangePartyComponent(MessagePayload<NetworkChangePartyComponent> payload)
    {
        var partyId = payload.What.PartyId;
        var componentId = payload.What.PartyComponentId;

        var party = ResolveAndRegister<MobileParty>(partyId);
        if (party == null) { Logger.Error("Failed to find party with stringId {stringId}", partyId); return; }

        var component = ResolveAndRegister<PartyComponent>(componentId);
        if (component == null) { Logger.Error("Failed to find PartyComponent with stringId {stringId}", componentId); return; }

        party._partyComponent = component;
    }

    private T ResolveAndRegister<T>(string id) where T : class
    {
        if (objectManager.TryGetObject<T>(id, out var obj)) return obj;
        object found = null;
        var com = Campaign.Current?.CampaignObjectManager;
        var mbo = MBObjectManager.Instance;
        try
        {
            if (com != null)
            {
                var mi = com.GetType().GetMethod("Find")?.MakeGenericMethod(typeof(T));
                if (mi != null) found = mi.Invoke(com, new object[] { id });
            }
        }
        catch { }
        if (found == null)
        {
            try
            {
                if (mbo != null)
                {
                    var containsMi = mbo.GetType().GetMethod("ContainsObject")?.MakeGenericMethod(typeof(T));
                    var getMi = mbo.GetType().GetMethod("GetObject")?.MakeGenericMethod(typeof(T));
                    if (containsMi != null && getMi != null)
                    {
                        var has = (bool)containsMi.Invoke(mbo, new object[] { id });
                        if (has) found = getMi.Invoke(mbo, new object[] { id });
                    }
                }
            }
            catch { }
        }
        if (found is T t)
        {
            if (objectManager.Contains(id) == false)
                objectManager.AddExisting(id, t);
            return t;
        }
        return null;
    }
}
