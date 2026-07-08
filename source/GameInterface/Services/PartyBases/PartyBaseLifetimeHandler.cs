using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.AutoSync;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyBases;

/// <summary>
/// Custom PartyBase lifetime replication (replaces the generic AutoRegistry constructor flow —
/// <see cref="PartyBaseRegistry"/>.Constructors is intentionally empty).
///
/// Why: on a client, a runtime-created MobileParty builds its own PartyBase in
/// <see cref="MobileParties.MobilePartyRegistry.OnClientCreated"/> (mirroring the vanilla ctor).
/// The generic flow then delivered a SECOND, skip-constructed PartyBase shell which the
/// <c>MobileParty.Party</c> AutoSync re-pointed onto the party — stranding everything keyed by
/// the original reference, most visibly <c>MobilePartyVisualManager._partiesAndVisuals</c>:
/// new parties had no working visual and dead ones left frozen ghosts. Instead, the creation
/// message carries the owning MobileParty's id and the client ADOPTS the PartyBase it already
/// built, registering it under the server's id. Settlement-owned or ownerless PartyBases still
/// get a shell, as before.
/// </summary>
internal class PartyBaseLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyBaseLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public PartyBaseLifetimeHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IAutoSyncPatchCollector patchCollector)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        // Every PartyBase creation funnels through the (MobileParty, Settlement) ctor (the
        // single-argument ctors chain to it). Queued via the collector so the prefix applies
        // after AutoSync code generation — attribute-patching a ctor that assigns AutoSync'd
        // properties silently kills their ctor-time syncs (setters inline before the detour).
        patchCollector.AddPrefix(
            AccessTools.Constructor(typeof(PartyBase), new[] { typeof(MobileParty), typeof(Settlement) }),
            AccessTools.Method(typeof(PartyBaseLifetimeHandler), nameof(CreatePrefix)));

        messageBroker.Subscribe<PartyBaseCreated>(Handle_PartyBaseCreated);
        messageBroker.Subscribe<NetworkCreatePartyBase>(Handle_NetworkCreatePartyBase);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyBaseCreated>(Handle_PartyBaseCreated);
        messageBroker.Unsubscribe<NetworkCreatePartyBase>(Handle_NetworkCreatePartyBase);
    }

    private static void CreatePrefix(ref PartyBase __instance, MobileParty mobileParty)
    {
        // Our own (client-side or replayed) construction runs without replication.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(PartyBase));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new PartyBaseCreated(__instance, mobileParty));
    }

    private void Handle_PartyBaseCreated(MessagePayload<PartyBaseCreated> payload)
    {
        var instance = payload.What.Instance;

        // Same live-create id scheme as the generic AutoRegistryHandler, so the join-time
        // id remap (RegisterAllObjects re-derivation) keeps working unchanged.
        var id = $"Created_{objectManager.GetUniqueTypeId(instance)}";
        if (!objectManager.AddExisting($"{nameof(PartyBase)}_{id}", instance))
        {
            Logger.Error("Unable to create new id for {type}", nameof(PartyBase));
            return;
        }

        string ownerId = null;
        if (payload.What.OwnerParty != null)
        {
            // The MobileParty ctor's creation prefix has already registered the party by the
            // time its body constructs the PartyBase, so this resolves for party-owned bases.
            objectManager.TryGetIdWithLogging(payload.What.OwnerParty, out ownerId);
        }

        network.SendAll(new NetworkCreatePartyBase(id, ownerId));
    }

    private void Handle_NetworkCreatePartyBase(MessagePayload<NetworkCreatePartyBase> payload)
    {
        var id = payload.What.PartyBaseId;

        PartyBase instance;
        if (payload.What.OwnerMobilePartyId != null
            && objectManager.TryGetObject(payload.What.OwnerMobilePartyId, out MobileParty owner)
            && owner.Party != null)
        {
            // Adopt the PartyBase this client already constructed for the party — the
            // MobileParty.Party AutoSync assignment then resolves to the same instance.
            instance = owner.Party;
        }
        else
        {
            // Settlement-owned / ownerless PartyBase: no local counterpart to adopt.
            instance = ObjectHelper.SkipConstructor<PartyBase>();
        }

        if (!objectManager.AddExisting($"{nameof(PartyBase)}_{id}", instance))
        {
            Logger.Error("Failed to register {type} with id {id}", nameof(PartyBase), id);
        }
    }
}
