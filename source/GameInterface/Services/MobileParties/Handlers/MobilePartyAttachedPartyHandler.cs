using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Handlers;


/// <summary>
/// Handler for attached mobile parties
/// </summary>
internal class MobilePartyAttachedPartyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAttachedPartyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public MobilePartyAttachedPartyHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<AddAttachedParty>(Handle_AddAttachedParty);
        messageBroker.Subscribe<RemoveAttachedParty>(Handle_RemoveAttachedParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddAttachedParty>(Handle_AddAttachedParty);
        messageBroker.Unsubscribe<RemoveAttachedParty>(Handle_RemoveAttachedParty);
    }

    private void Handle_RemoveAttachedParty(MessagePayload<RemoveAttachedParty> payload)
    {
        var data = payload.What.AttachedPartyData;
        var instanceId = data.PartyId;
        var removedPartyId = data.ListPartyId;

        var instance = ResolveAndRegister<MobileParty>(instanceId);
        if (instance == null) { Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), instanceId); return; }

        var removedParty = ResolveAndRegister<MobileParty>(removedPartyId);
        if (removedParty == null) { Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), removedPartyId); return; }

        instance._attachedParties.Remove(removedParty);
    }

    private void Handle_AddAttachedParty(MessagePayload<AddAttachedParty> payload)
    {
        var data = payload.What.AttachedPartyData;
        var instanceId = data.PartyId;
        var addedPartyId = data.ListPartyId;

        var instance = ResolveAndRegister<MobileParty>(instanceId);
        if (instance == null) { Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), instanceId); return; }

        var addedParty = ResolveAndRegister<MobileParty>(addedPartyId);
        if (addedParty == null) { Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), addedPartyId); return; }

        instance._attachedParties.Add(addedParty);
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
