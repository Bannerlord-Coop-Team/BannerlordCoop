using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Ferries.Interfaces;
using GameInterface.Services.Ferries.Messages;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.NavalDLC.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Ferries.Handlers;

/// <summary>
/// Lands a player at the end of their ferry trip on the server, and puts a rejoining player back on their ferry.
/// </summary>
internal class PlayerFerryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerFerryInterface ferryInterface;

    private NavalPlayerData navalPlayerData;

    public PlayerFerryHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IPlayerFerryInterface ferryInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.ferryInterface = ferryInterface;

        messageBroker.Subscribe<PlayerFerryEndAttempted>(Handle_PlayerFerryEndAttempted);
        messageBroker.Subscribe<NetworkRequestEndFerry>(Handle_NetworkRequestEndFerry);
        messageBroker.Subscribe<InitializeClientNavalData>(Handle_InitializeClientNavalData);
        messageBroker.Subscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerFerryEndAttempted>(Handle_PlayerFerryEndAttempted);
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

    private void Handle_InitializeClientNavalData(MessagePayload<InitializeClientNavalData> obj)
    {
        navalPlayerData = obj.What.NavalPlayerData;
    }

    // The server's save has no PlayerDataForNavalAutoTravel for this player, so a rejoin mid trip boards the ferry again
    private void Handle_SwitchedPlayer(MessagePayload<SwitchedPlayer> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (navalPlayerData?.PlayerFerryDepartures == null) return;
            if (!objectManager.TryGetId(Hero.MainHero, out var playerHeroId)) return;
            if (!navalPlayerData.PlayerFerryDepartures.TryGetValue(playerHeroId, out var villageId)) return;

            if (!objectManager.TryGetObjectWithLogging<Settlement>(villageId, out var village) || village.FerryTarget == null) return;

            ferryInterface.BoardLocally(village);
        });
    }
}
