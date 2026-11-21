using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Ferries.Interfaces;
using GameInterface.Services.Ferries.Messages;
using GameInterface.Services.NavalDLC.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Ferries.Handlers;

/// <summary>
/// Lands a player at the end of their ferry trip on the server, tells every client who is on a ferry, and puts a rejoining
/// player back on their ferry.
/// </summary>
internal class PlayerFerryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerFerryInterface ferryInterface;
    private readonly IPlayerFerryTracker ferryTracker;

    public PlayerFerryHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IPlayerFerryInterface ferryInterface,
        IPlayerFerryTracker ferryTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.ferryInterface = ferryInterface;
        this.ferryTracker = ferryTracker;

        messageBroker.Subscribe<PlayerFerryEndAttempted>(Handle_PlayerFerryEndAttempted);
        messageBroker.Subscribe<PlayerFerryChanged>(Handle_PlayerFerryChanged);
        messageBroker.Subscribe<NetworkPlayerFerryChanged>(Handle_NetworkPlayerFerryChanged);
        messageBroker.Subscribe<NetworkRequestEndFerry>(Handle_NetworkRequestEndFerry);
        messageBroker.Subscribe<InitializeClientNavalData>(Handle_InitializeClientNavalData);
        messageBroker.Subscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerFerryEndAttempted>(Handle_PlayerFerryEndAttempted);
        messageBroker.Unsubscribe<PlayerFerryChanged>(Handle_PlayerFerryChanged);
        messageBroker.Unsubscribe<NetworkPlayerFerryChanged>(Handle_NetworkPlayerFerryChanged);
        messageBroker.Unsubscribe<NetworkRequestEndFerry>(Handle_NetworkRequestEndFerry);
        messageBroker.Unsubscribe<InitializeClientNavalData>(Handle_InitializeClientNavalData);
        messageBroker.Unsubscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
    }

    private void Handle_PlayerFerryEndAttempted(MessagePayload<PlayerFerryEndAttempted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.PlayerParty, out var partyId)) return;

        network.SendAll(new NetworkRequestEndFerry(partyId));
    }

    private void Handle_NetworkRequestEndFerry(MessagePayload<NetworkRequestEndFerry> obj)
    {
        var partyId = obj.What.PartyId;
        if (!playerManager.IsSenderControllingParty(obj.Who, partyId)) return;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var party)) return;

            // A second Continue finds the party already landed
            ferryInterface.TryEndFerry(party);
        });
    }

    private void Handle_PlayerFerryChanged(MessagePayload<PlayerFerryChanged> obj)
    {
        network.SendAll(new NetworkPlayerFerryChanged(obj.What.PlayerHeroId, obj.What.VillageId));
    }

    // Vanilla only knows the main party's ferry, so the client keeps every player's ferry in IPlayerFerryTracker and redraws that party's ship
    private void Handle_NetworkPlayerFerryChanged(MessagePayload<NetworkPlayerFerryChanged> obj)
    {
        var payload = obj.What;

        GameThread.RunSafe(() =>
        {
            ferryTracker.Set(payload.PlayerHeroId, payload.VillageId);

            if (!objectManager.TryGetObjectWithLogging<Hero>(payload.PlayerHeroId, out var playerHero)) return;

            playerHero.PartyBelongedTo?.SetNavalVisualAsDirty();
        });
    }

    private void Handle_InitializeClientNavalData(MessagePayload<InitializeClientNavalData> obj)
    {
        // Reset on the game thread, because IsInFerryState reads the tracker there every tick
        var departures = obj.What.NavalPlayerData?.PlayerFerryDepartures;
        GameThread.RunSafe(() => ferryTracker.Reset(departures));
    }

    // The server's save has no PlayerDataForNavalAutoTravel for this player, so a rejoin mid trip boards the ferry again
    private void Handle_SwitchedPlayer(MessagePayload<SwitchedPlayer> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetId(Hero.MainHero, out var playerHeroId)) return;
            if (!ferryTracker.Departures.TryGetValue(playerHeroId, out var villageId)) return;

            if (!objectManager.TryGetObjectWithLogging<Settlement>(villageId, out var village) || village.FerryTarget == null) return;

            ferryInterface.BoardLocally(village);
        });
    }
}
