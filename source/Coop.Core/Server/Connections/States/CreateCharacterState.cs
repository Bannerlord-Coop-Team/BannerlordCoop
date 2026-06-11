using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection creating a character
/// </summary>
public class CreateCharacterState : ConnectionStateBase
{
    private readonly ILogger Logger = LogManager.GetLogger<CreateCharacterState>();
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IHeroInterface heroInterface;
    private readonly IPlayerManager playerRegistry;

    public CreateCharacterState(
        IConnectionLogic connectionLogic,
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        INetwork network,
        IHeroInterface heroInterface,
        IPlayerManager playerRegistry)
        : base(connectionLogic)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.network = network;
        this.heroInterface = heroInterface;
        this.playerRegistry = playerRegistry;
        messageBroker.Subscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
    }

    internal void Handle_NetworkTransferNewHero(MessagePayload<NetworkTransferNewHero> obj)
    {
        var netPeer = obj.Who as NetPeer;

        if (netPeer != ConnectionLogic.Peer) return;

        var controllerId = obj.What.PlayerId;
        var data = obj.What.PlayerHero;

        Logger.Debug("Unpacking hero for {ControllerId}", controllerId);

        var hero = heroInterface.ServerUnpackHero(data);

        if (!TryCreatePlayer(controllerId, hero, out var player))
        {
            Logger.Error("Failed to create player");
            return;
        }

        if (!playerRegistry.AddPlayer(player))
            Logger.Error("Player has been already added.");

        // Send created to all other clients
        var message = new NetworkNewPlayerHeroCreated(controllerId, player, data);
        network.SendAllBut(netPeer, message);

        // Respond with ids for the creating client
        network.Send(netPeer, new NetworkHeroRecieved(player));

        ConnectionLogic.TransferSave();
    }

    private bool TryCreatePlayer(string controllerId, Hero hero, out Player player)
    {
        player = null;

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId))
            return false;
        if (!objectManager.TryGetIdWithLogging(hero.PartyBelongedTo, out var mobilePartyId))
            return false;
        if (!objectManager.TryGetIdWithLogging(hero.Clan, out var clanId))
            return false;
        if (!objectManager.TryGetIdWithLogging(hero.CharacterObject, out var characterObjectId))
            return false;

        player = new Player(controllerId, heroId, mobilePartyId, clanId, characterObjectId);
        return true;
    }

    public override void CreateCharacter()
    {
    }

    public override void EnterCampaign()
    {
    }

    public override void EnterMission()
    {
    }

    public override void Load()
    {
    }

    public override void TransferSave()
    {
        ConnectionLogic.SetState<TransferSaveState>();
    }
}
