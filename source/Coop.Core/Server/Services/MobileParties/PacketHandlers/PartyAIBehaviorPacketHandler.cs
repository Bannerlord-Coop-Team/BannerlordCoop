using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.PacketHandlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using static GameInterface.Services.ObjectManager.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.MobileParties.PacketHandlers;

/// <summary>
/// Handles incoming <see cref="RequestMobilePartyBehaviorPacket"/>
/// </summary>
internal class RequestMobilePartyBehaviorPacketHandler : IPacketHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RequestMobilePartyBehaviorPacketHandler>();

    // Coalescer channel for per-party behavior updates; only the latest behavior per party is sent each tick.
    private const string PartyBehaviorUpdateChannel = "PartyBehaviorUpdate";

    public PacketType PacketType => PacketType.RequestUpdatePartyBehavior;

    private readonly IPacketManager packetManager;
    private readonly ISendCoalescer coalescer;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot;
    private readonly IPlayerManager playerManager;

    public RequestMobilePartyBehaviorPacketHandler(
        IPacketManager packetManager,
        ISendCoalescer coalescer,
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot,
        IPlayerManager playerManager)
    {
        this.packetManager = packetManager;
        this.coalescer = coalescer;
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mobilePartyBehaviorSnapshot = mobilePartyBehaviorSnapshot;
        this.playerManager = playerManager;
        packetManager.RegisterPacketHandler(this);

        messageBroker.Subscribe<PartyBehaviorUpdated>(Handle_PartyBehaviorUpdated);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);

        messageBroker.Unsubscribe<PartyBehaviorUpdated>(Handle_PartyBehaviorUpdated);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        if (peer == null ||
            !playerManager.TryGetPlayer(peer, out var player) ||
            string.IsNullOrWhiteSpace(player.ControllerId))
        {
            Reject(peer, "the peer is not mapped to an authenticated player");
            return;
        }

        if (packet is not RequestMobilePartyBehaviorPacket convertedPacket)
        {
            Reject(peer, "the packet payload has the wrong type");
            return;
        }

        var controlledPartyId = player.MobilePartyId;
        var controllerId = player.ControllerId;
        var data = convertedPacket.BehaviorUpdateData;
        GameThread.RunSafe(
            () => ValidateAndPublishRequest(peer, controlledPartyId, controllerId, data),
            blocking: true,
            context: nameof(HandlePacket));
    }

    private void ValidateAndPublishRequest(
        NetPeer peer,
        string controlledPartyId,
        string controllerId,
        PartyBehaviorUpdateData data)
    {
        if (!TryValidateClientSnapshot(controlledPartyId, data, out string reason))
        {
            Reject(peer, reason);
            return;
        }

        // Both fields are server-authoritative. A client may predict its own movement, but it may
        // neither spoof whose prediction this is nor force an immediate position/queue flush.
        data.OriginControllerId = controllerId;
        data.ForcePosition = false;

        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
    }

    private bool TryValidateClientSnapshot(
        string controlledPartyId,
        PartyBehaviorUpdateData data,
        out string reason)
    {
        reason = null;

        if (string.IsNullOrWhiteSpace(controlledPartyId) ||
            string.IsNullOrWhiteSpace(data.MobilePartyId) ||
            !SamePartyId(controlledPartyId, data.MobilePartyId))
        {
            reason = "the requested party is not controlled by the peer";
            return false;
        }

        if (!objectManager.TryGetObject(data.MobilePartyId, out MobileParty _))
        {
            reason = "the controlled party does not exist";
            return false;
        }

        if (!IsValidAiBehavior(data.NewAiBehavior) ||
            !IsValidAiBehavior(data.DefaultBehavior) ||
            !Enum.IsDefined(typeof(MobileParty.NavigationType), data.DesiredAiNavigationType) ||
            !Enum.IsDefined(typeof(MoveModeType), data.PartyMoveMode) ||
            !Enum.IsDefined(typeof(BehaviorInteractableKind), data.InteractableKind))
        {
            reason = "the snapshot contains an unknown behavior or movement enum";
            return false;
        }

        if (!IsFinite(data.BestTargetPoint) ||
            !IsFinite(data.PartyPosition) ||
            !IsFinite(data.TargetPosition) ||
            !IsFinite(data.MoveTargetPoint) ||
            !IsFinite(data.NextTargetPosition))
        {
            reason = "the snapshot contains a non-finite map position";
            return false;
        }

        if (!TryValidateInteractable(data, out PartyBase interactablePartyBase))
        {
            reason = "the behavior interactable is inconsistent or does not exist";
            return false;
        }

        if (!TryResolveOptional(data.TargetPartyId, out MobileParty targetParty) ||
            !TryResolveOptional(data.TargetSettlementId, out Settlement targetSettlement) ||
            !TryResolveOptional(data.MoveTargetPartyId, out MobileParty moveTargetParty))
        {
            reason = "the snapshot contains an invalid target reference";
            return false;
        }

        if (RequiresTargetParty(data.DefaultBehavior) && targetParty == null)
        {
            reason = "the default behavior requires a target party";
            return false;
        }

        if (RequiresTargetSettlement(data.DefaultBehavior) && targetSettlement == null)
        {
            reason = "the default behavior requires a target settlement";
            return false;
        }

        if (data.IsTargetingPort && targetSettlement == null)
        {
            reason = "port movement requires a target settlement";
            return false;
        }

        if ((data.PartyMoveMode == MoveModeType.Party) != (moveTargetParty != null))
        {
            reason = "party navigation and its movement target disagree";
            return false;
        }

        if (!TryValidateShortTermBehavior(
                data,
                interactablePartyBase,
                moveTargetParty))
        {
            reason = "the short-term behavior and its target references disagree";
            return false;
        }

        return true;
    }

    private bool TryValidateInteractable(
        PartyBehaviorUpdateData data,
        out PartyBase partyBase)
    {
        partyBase = null;

        if (!data.HasTarget)
            return string.IsNullOrEmpty(data.InteractablePointId);

        if (string.IsNullOrWhiteSpace(data.InteractablePointId))
            return false;

        return data.InteractableKind switch
        {
            BehaviorInteractableKind.PartyBase =>
                objectManager.TryGetObject(data.InteractablePointId, out partyBase),
            BehaviorInteractableKind.AnchorPoint =>
                objectManager.TryGetObject(data.InteractablePointId, out MobileParty owner) &&
                owner.Anchor != null,
            _ => false,
        };
    }

    private bool TryResolveOptional<T>(string id, out T value)
        where T : class
    {
        value = null;
        if (string.IsNullOrEmpty(id))
            return true;

        return !string.IsNullOrWhiteSpace(id) && objectManager.TryGetObject(id, out value);
    }

    private static bool TryValidateShortTermBehavior(
        PartyBehaviorUpdateData data,
        PartyBase interactablePartyBase,
        MobileParty moveTargetParty)
    {
        if (RequiresInteractablePartyBase(data.NewAiBehavior))
            return interactablePartyBase?.IsValid == true;

        if (data.NewAiBehavior != AiBehavior.EngageParty)
            return true;

        var engagedParty = interactablePartyBase?.MobileParty;
        return engagedParty != null && ReferenceEquals(engagedParty, moveTargetParty);
    }

    private static bool SamePartyId(string left, string right) =>
        string.Equals(
            Compact(left, typeof(MobileParty)),
            Compact(right, typeof(MobileParty)),
            StringComparison.Ordinal);

    private static bool IsFinite(CampaignVec2 point) =>
        !float.IsNaN(point.X) &&
        !float.IsInfinity(point.X) &&
        !float.IsNaN(point.Y) &&
        !float.IsInfinity(point.Y);

    private static bool IsValidAiBehavior(AiBehavior behavior) =>
        behavior != AiBehavior.NumAiBehaviors &&
        Enum.IsDefined(typeof(AiBehavior), behavior);

    private static bool RequiresTargetParty(AiBehavior behavior) =>
        behavior == AiBehavior.EngageParty ||
        behavior == AiBehavior.GoAroundParty ||
        behavior == AiBehavior.EscortParty;

    private static bool RequiresTargetSettlement(AiBehavior behavior) =>
        behavior == AiBehavior.GoToSettlement ||
        behavior == AiBehavior.RaidSettlement ||
        behavior == AiBehavior.BesiegeSettlement ||
        behavior == AiBehavior.DefendSettlement;

    private static bool RequiresInteractablePartyBase(AiBehavior behavior) =>
        behavior == AiBehavior.GoToSettlement ||
        behavior == AiBehavior.RaidSettlement ||
        behavior == AiBehavior.AssaultSettlement ||
        behavior == AiBehavior.BesiegeSettlement;

    private static void Reject(NetPeer peer, string reason)
    {
        Logger.Warning(
            "Rejected mobile-party behavior request from peer {PeerId}: {Reason}",
            peer?.Id,
            reason);
    }

    private void Handle_PartyBehaviorUpdated(MessagePayload<PartyBehaviorUpdated> payload)
    {
        var data = payload.What.BehaviorUpdateData;

        // Every producer converges here. Rebuild from the authoritative server object so a client
        // request, a forced battle-finalization update, and a SetMove* capture all broadcast the
        // same complete state instead of echoing a partial or stale request payload.
        if (!objectManager.TryGetObject(data.MobilePartyId, out MobileParty party) ||
            !mobilePartyBehaviorSnapshot.TryCreateCurrent(
                party,
                out var authoritativeData))
            return;

        authoritativeData.OriginControllerId = data.OriginControllerId;
        authoritativeData.ForcePosition = data.ForcePosition;
        data = authoritativeData;

        // Coalesce per party: repeated behavior changes to one party collapse into a single latest-wins send per tick.
        var key = new CoalesceKey(PartyBehaviorUpdateChannel, Compact(data.MobilePartyId, typeof(MobileParty)));
        coalescer.Enqueue(key, new LatestWinsPayload(new NetworkUpdatePartyBehavior(data)));

        if (data.ForcePosition)
            coalescer.FlushInstance(key.InstanceId, network);
    }
}
