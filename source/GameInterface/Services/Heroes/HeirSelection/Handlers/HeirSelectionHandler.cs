using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.Heroes.HeirSelection.Handlers;

internal class HeirSelectionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeirSelectionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IApplyHeirSelectionActionInterface applyHeirSelectionActionInterface;

    public HeirSelectionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IApplyHeirSelectionActionInterface applyHeirSelectionActionInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.applyHeirSelectionActionInterface = applyHeirSelectionActionInterface;

        messageBroker.Subscribe<ClientSelectHeir>(Handle_ClientSelectHeir);
        messageBroker.Subscribe<NetworkClientSelectHeir>(Handle_NetworkClientSelectHeir);

        messageBroker.Subscribe<HeirSelectionOver>(Handle_HeirSelectionOver);
        messageBroker.Subscribe<NetworkHeirSelectionOver>(Handle_NetworkHeirSelectionOver);

        messageBroker.Subscribe<ChangePlayerCharacterAfterHeirSelection>(Handle_ChangePlayerCharacterAfterHeirSelection);
        messageBroker.Subscribe<NetworkChangePlayerCharacterAfterHeirSelection>(Handle_NetworkChangePlayerCharacterAfterHeirSelection);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientSelectHeir>(Handle_ClientSelectHeir);
        messageBroker.Unsubscribe<NetworkClientSelectHeir>(Handle_NetworkClientSelectHeir);

        messageBroker.Unsubscribe<HeirSelectionOver>(Handle_HeirSelectionOver);
        messageBroker.Unsubscribe<NetworkHeirSelectionOver>(Handle_NetworkHeirSelectionOver);

        messageBroker.Unsubscribe<ChangePlayerCharacterAfterHeirSelection>(Handle_ChangePlayerCharacterAfterHeirSelection);
        messageBroker.Unsubscribe<NetworkChangePlayerCharacterAfterHeirSelection>(Handle_NetworkChangePlayerCharacterAfterHeirSelection);
    }

    private void Handle_ClientSelectHeir(MessagePayload<ClientSelectHeir> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.PlayerVictim, out var playerVictimId)) return;
        if (!TryGetPeerForHero(playerVictimId, out var peer)) return;

        var heirIdApparents = new Dictionary<string, int>();
        foreach (var heirApparent in data.HeirApparents)
        {
            if (!objectManager.TryGetIdWithLogging(heirApparent.Key, out var heirHeroId)) continue;

            heirIdApparents[heirHeroId] = heirApparent.Value;
        }

        network.Send(peer, new NetworkClientSelectHeir(heirIdApparents));
    }

    private void Handle_NetworkClientSelectHeir(MessagePayload<NetworkClientSelectHeir> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            var heirApparents = new Dictionary<Hero, int>();
            foreach (var heirIdApparent in data.HeirIdApparents)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(heirIdApparent.Key, out var heirHero)) continue;

                heirApparents[heirHero] = heirIdApparent.Value;
            }

            if (PlayerEncounter.Current != null && (PlayerEncounter.Battle == null || !PlayerEncounter.Battle.IsFinalized))
            {
                PlayerEncounter.Finish(true);
            }
            CampaignEventDispatcher.Instance.OnHeirSelectionRequested(heirApparents);
        });
    }

    private void Handle_HeirSelectionOver(MessagePayload<HeirSelectionOver> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.OriginalHero, out var originalHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.SelectedHeir, out var selectedHeirId)) return;

        network.SendAll(new NetworkHeirSelectionOver(originalHeroId, selectedHeirId));
    }

    private void Handle_NetworkHeirSelectionOver(MessagePayload<NetworkHeirSelectionOver> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OriginalHeroId, out var originalHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.SelectedHeirId, out var selectedHeir)) return;

            applyHeirSelectionActionInterface.ApplyByDeath(originalHero, selectedHeir);
        });
    }

    private void Handle_ChangePlayerCharacterAfterHeirSelection(MessagePayload<ChangePlayerCharacterAfterHeirSelection> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.OriginalHero, out var originalHeroId)) return;
        if (!TryGetPeerForHero(originalHeroId, out var peer)) return;
        if (!objectManager.TryGetIdWithLogging(data.Heir, out var heirId)) return;

        network.Send(peer, new NetworkChangePlayerCharacterAfterHeirSelection(heirId));
    }

    private void Handle_NetworkChangePlayerCharacterAfterHeirSelection(MessagePayload<NetworkChangePlayerCharacterAfterHeirSelection> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeirId, out var heir)) return;

            ChangePlayerCharacterAction.Apply(heir);
        });
    }

    private bool TryGetPeerForHero(string playerHeroId, out NetPeer peer)
    {
        peer = null;

        foreach (var player in playerManager.Players)
        {
            if (player.HeroId != playerHeroId) continue;

            return playerManager.TryGetPeer(player.ControllerId, out peer);
        }

        Logger.Error($"Failed to get peer for player hero with id {playerHeroId} during heir selection");
        return false;
    }
}
