using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// Routes the player's alley management actions (abandon, change overseer, manage garrison) as
/// client to server requests, performs them authoritatively on the server with patches live, and
/// replicates the resulting garrison/overseer state to the owning client so its in-game menus stay
/// correct. The authoritative garrison/overseer is held in the CoopSession via
/// <see cref="ISessionAlleyPlayerDataInterface"/>; the owning client mirror lives in the behavior
/// via <see cref="IAlleyCampaignBehaviorInterface"/>.
/// </summary>
internal class AlleyManagementHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleyManagementHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionAlleyPlayerDataInterface sessionInterface;
    private readonly IAlleyCampaignBehaviorInterface behaviorInterface;

    public AlleyManagementHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionAlleyPlayerDataInterface sessionInterface,
        IAlleyCampaignBehaviorInterface behaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInterface = sessionInterface;
        this.behaviorInterface = behaviorInterface;

        messageBroker.Subscribe<AlleyAcquiredRequested>(Handle_AlleyAcquiredRequested);
        messageBroker.Subscribe<AbandonAlleyRequested>(Handle_AbandonAlleyRequested);
        messageBroker.Subscribe<ChangeAlleyOverseerRequested>(Handle_ChangeAlleyOverseerRequested);
        messageBroker.Subscribe<SetAlleyGarrisonRequested>(Handle_SetAlleyGarrisonRequested);

        messageBroker.Subscribe<RequestAcquireAlley>(Handle_RequestAcquireAlley);
        messageBroker.Subscribe<RequestAbandonAlley>(Handle_RequestAbandonAlley);
        messageBroker.Subscribe<RequestChangeAlleyOverseer>(Handle_RequestChangeAlleyOverseer);
        messageBroker.Subscribe<RequestSetAlleyGarrison>(Handle_RequestSetAlleyGarrison);

        messageBroker.Subscribe<NetworkAlleyManagementUpdated>(Handle_NetworkAlleyManagementUpdated);
        messageBroker.Subscribe<NetworkAlleyManagementRemoved>(Handle_NetworkAlleyManagementRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AlleyAcquiredRequested>(Handle_AlleyAcquiredRequested);
        messageBroker.Unsubscribe<AbandonAlleyRequested>(Handle_AbandonAlleyRequested);
        messageBroker.Unsubscribe<ChangeAlleyOverseerRequested>(Handle_ChangeAlleyOverseerRequested);
        messageBroker.Unsubscribe<SetAlleyGarrisonRequested>(Handle_SetAlleyGarrisonRequested);

        messageBroker.Unsubscribe<RequestAcquireAlley>(Handle_RequestAcquireAlley);
        messageBroker.Unsubscribe<RequestAbandonAlley>(Handle_RequestAbandonAlley);
        messageBroker.Unsubscribe<RequestChangeAlleyOverseer>(Handle_RequestChangeAlleyOverseer);
        messageBroker.Unsubscribe<RequestSetAlleyGarrison>(Handle_RequestSetAlleyGarrison);

        messageBroker.Unsubscribe<NetworkAlleyManagementUpdated>(Handle_NetworkAlleyManagementUpdated);
        messageBroker.Unsubscribe<NetworkAlleyManagementRemoved>(Handle_NetworkAlleyManagementRemoved);
    }

    // --- Local requests (requesting client) -> network request to the server ---

    private void Handle_AlleyAcquiredRequested(MessagePayload<AlleyAcquiredRequested> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Alley, out var alleyId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Overseer, out var overseerId)) return;

        network.SendAll(new RequestAcquireAlley(alleyId, ownerId, overseerId, AlleyGarrisonData.ToData(payload.What.Garrison, objectManager)));
    }

    private void Handle_AbandonAlleyRequested(MessagePayload<AbandonAlleyRequested> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Alley, out var alleyId)) return;

        network.SendAll(new RequestAbandonAlley(alleyId, payload.What.FromClanScreen));
    }

    private void Handle_ChangeAlleyOverseerRequested(MessagePayload<ChangeAlleyOverseerRequested> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Alley, out var alleyId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.NewOverseer, out var overseerId)) return;

        network.SendAll(new RequestChangeAlleyOverseer(alleyId, overseerId));
    }

    private void Handle_SetAlleyGarrisonRequested(MessagePayload<SetAlleyGarrisonRequested> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Alley, out var alleyId)) return;

        network.SendAll(new RequestSetAlleyGarrison(alleyId, AlleyGarrisonData.ToData(payload.What.NewGarrison, objectManager)));
    }

    // --- Network requests (server, authoritative) ---

    private void Handle_RequestAcquireAlley(MessagePayload<RequestAcquireAlley> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OverseerId, out var overseer)) return;

            var garrison = data.Garrison ?? new TroopRosterElementData[0];

            // The take-over is authoritative: the alley is owned by the acquiring player (owner) and
            // run by the chosen clan member (overseer). The garrison + overseer are stored, the overseer
            // travels to the alley settlement, and the result is broadcast so the owning client's
            // manage-alley menus light up (its alley.Owner == its main hero).
            var displacedOwner = alley.Owner;
            alley.SetOwner(owner);
            ApplyAcquisitionRelationPenalties(owner, displacedOwner, alley.Settlement);
            sessionInterface.SetManagementData(data.AlleyId, data.OverseerId, garrison);
            TeleportOverseerToAlley(overseer, alley);

            network.SendAll(new NetworkAlleyManagementUpdated(data.AlleyId, data.OverseerId, garrison));
        });
    }

    /// <summary>
    /// Replays vanilla OnAlleyOccupiedByPlayer's relation hits (server authoritative; relation is not
    /// itself networked): the displaced gang leader loses 5, and if the settlement isn't the new owner's
    /// own, its owner loses 2 and its non-gang-leader notables lose 1 each. Keyed off the acquiring
    /// owner rather than Hero.MainHero, which is null on the host.
    /// </summary>
    private static void ApplyAcquisitionRelationPenalties(Hero owner, Hero displacedOwner, Settlement settlement)
    {
        if (owner == null) return;

        if (displacedOwner != null && displacedOwner != owner)
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(owner, displacedOwner, -5, showQuickNotification: false);
        }

        if (settlement == null || settlement.OwnerClan == owner.Clan) return;

        if (settlement.Owner != null)
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(owner, settlement.Owner, -2, showQuickNotification: false);
        }

        if (settlement.Notables == null) return;
        foreach (var notable in settlement.Notables)
        {
            if (notable != null && !notable.IsGangLeader)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(owner, notable, -1, showQuickNotification: false);
            }
        }
    }

    /// <summary>
    /// Sends the overseer to the alley's settlement to run it, the way vanilla does - but never a
    /// player-controlled hero: a player must stay free to move on the map and is never pinned to a
    /// settlement just for being assigned to an alley.
    /// </summary>
    private static void TeleportOverseerToAlley(Hero overseer, Alley alley)
    {
        if (overseer == null || overseer.IsPlayerHero()) return;
        TeleportHeroAction.ApplyDelayedTeleportToSettlement(overseer, alley.Settlement);
    }

    private void Handle_RequestAbandonAlley(MessagePayload<RequestAbandonAlley> payload)
    {
        if (ModInformation.IsClient) return;

        var alleyId = payload.What.AlleyId;
        var fromClanScreen = payload.What.FromClanScreen;
        GameThread.RunSafe(() => AbandonAlley(alleyId, fromClanScreen));
    }

    private void Handle_RequestChangeAlleyOverseer(MessagePayload<RequestChangeAlleyOverseer> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.NewOverseerId, out var newOverseer)) return;

            // Swap the overseer hero in the stored garrison the way vanilla ChangeTheLeaderOfAlleyInternal does.
            sessionInterface.TryGetManagementData(data.AlleyId, out var stored);
            var garrison = SwapOverseerInGarrison(stored?.Garrison, stored?.OverseerId, data.NewOverseerId);

            sessionInterface.SetManagementData(data.AlleyId, data.NewOverseerId, garrison);
            TeleportOverseerToAlley(newOverseer, alley);

            network.SendAll(new NetworkAlleyManagementUpdated(data.AlleyId, data.NewOverseerId, garrison));
        });
    }

    private void Handle_RequestSetAlleyGarrison(MessagePayload<RequestSetAlleyGarrison> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out _)) return;

            var newGarrison = data.Garrison ?? new TroopRosterElementData[0];
            // The alley party screen already moved the troops between the owner's party and the alley
            // roster, and that party-roster change replicates through the existing party-screen sync, so
            // the server only records the new garrison snapshot (for persistence and abandon-return).
            sessionInterface.TryGetManagementData(data.AlleyId, out var stored);
            sessionInterface.SetManagementData(data.AlleyId, stored?.OverseerId, newGarrison);

            network.SendAll(new NetworkAlleyManagementUpdated(data.AlleyId, stored?.OverseerId, newGarrison));
        });
    }

    // --- Network broadcasts (client apply) ---

    private void Handle_NetworkAlleyManagementUpdated(MessagePayload<NetworkAlleyManagementUpdated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;

            // Only the owning client keeps the behavior-side management data. A client that no longer
            // owns this alley (ownership transferred away without an abandon) drops its stale copy so
            // its manage-alley menus don't linger.
            if (alley.Owner != Hero.MainHero)
            {
                behaviorInterface.RemovePlayerAlleyData(alley);
                return;
            }

            Hero overseer = null;
            if (data.OverseerId != null) objectManager.TryGetObjectWithLogging(data.OverseerId, out overseer);

            behaviorInterface.AddOrUpdatePlayerAlleyData(alley, overseer, AlleyGarrisonData.FromData(data.Garrison, objectManager));
        });
    }

    private void Handle_NetworkAlleyManagementRemoved(MessagePayload<NetworkAlleyManagementRemoved> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;
            behaviorInterface.RemovePlayerAlleyData(alley);
        });
    }

    /// <summary>
    /// Authoritative abandon: return the stored garrison troops to the owner's party, clear the
    /// owner, drop the stored management data and tell clients to remove their mirror.
    /// </summary>
    internal void AbandonAlley(string alleyId, bool fromClanScreen)
    {
        if (!objectManager.TryGetObjectWithLogging<Alley>(alleyId, out var alley)) return;

        // A menu/dialog abandon returns the garrison troops to the owner's party; a clan-screen
        // abandon forfeits them, matching vanilla AbandonTheAlley(fromClanScreen).
        if (!fromClanScreen && sessionInterface.TryGetManagementData(alleyId, out var data))
        {
            ReturnGarrisonToOwner(alley.Owner, data.Garrison);
        }

        alley.SetOwner(null);
        sessionInterface.RemoveManagementData(alleyId);
        network.SendAll(new NetworkAlleyManagementRemoved(alleyId));
    }

    private void ReturnGarrisonToOwner(Hero owner, TroopRosterElementData[] garrison)
    {
        if (garrison == null) return;

        var party = owner?.PartyBelongedTo;
        if (party == null)
        {
            // No party to return the garrison to (the owner isn't leading one); surface it rather than
            // silently dropping the troops.
            Logger.Error("Could not return alley garrison: owner {owner} has no party", owner?.StringId);
            return;
        }

        foreach (var element in garrison)
        {
            if (!objectManager.TryGetObject<CharacterObject>(element.CharacterId, out var character)) continue;
            if (character.IsHero) continue;
            party.MemberRoster.AddToCounts(character, element.Number, false, element.WoundedNumber, element.Xp, true, -1);
        }
    }

    /// <summary>
    /// Returns the stored garrison with the overseer hero swapped (old removed, new added), mirroring
    /// vanilla ChangeTheLeaderOfAlleyInternal so the stored roster reflects the current overseer.
    /// </summary>
    private TroopRosterElementData[] SwapOverseerInGarrison(TroopRosterElementData[] garrison, string oldOverseerId, string newOverseerId)
    {
        var list = new List<TroopRosterElementData>(garrison ?? new TroopRosterElementData[0]);

        if (TryGetHeroCharacterId(oldOverseerId, out var oldCharId))
            list.RemoveAll(e => e.CharacterId == oldCharId);

        if (TryGetHeroCharacterId(newOverseerId, out var newCharId) && !list.Exists(e => e.CharacterId == newCharId))
            list.Add(new TroopRosterElementData(newCharId, 1, 0, 0));

        return list.ToArray();
    }

    private bool TryGetHeroCharacterId(string heroId, out string characterId)
    {
        characterId = null;
        if (heroId == null) return false;
        if (!objectManager.TryGetObject<Hero>(heroId, out var hero) || hero.CharacterObject == null) return false;
        return objectManager.TryGetId(hero.CharacterObject, out characterId);
    }
}
