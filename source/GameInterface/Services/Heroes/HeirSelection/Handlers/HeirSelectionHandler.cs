using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.HeirSelection.Handlers;

internal class HeirSelectionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeirSelectionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerPartyRestorer playerPartyRestorer;
    private readonly IApplyHeirSelectionActionInterface applyHeirSelectionActionInterface;
    private readonly IHeirSelectionCampaignBehaviorInterface heirSelectionCampaignBehaviorInterface;

    public HeirSelectionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IPlayerPartyRestorer playerPartyRestorer,
        IApplyHeirSelectionActionInterface applyHeirSelectionActionInterface,
        IHeirSelectionCampaignBehaviorInterface heirSelectionCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.playerPartyRestorer = playerPartyRestorer;
        this.applyHeirSelectionActionInterface = applyHeirSelectionActionInterface;
        this.heirSelectionCampaignBehaviorInterface = heirSelectionCampaignBehaviorInterface;

        messageBroker.Subscribe<PlayerHeirSelectionRequested>(Handle_PlayerHeirSelectionRequested);
        messageBroker.Subscribe<NetworkClientSelectHeir>(Handle_NetworkClientSelectHeir);

        messageBroker.Subscribe<HeirSelectionOver>(Handle_HeirSelectionOver);
        messageBroker.Subscribe<NetworkHeirSelectionOver>(Handle_NetworkHeirSelectionOver);

        messageBroker.Subscribe<ChangePlayerCharacterAfterHeirSelection>(Handle_ChangePlayerCharacterAfterHeirSelection);
        messageBroker.Subscribe<NetworkChangePlayerCharacterAfterHeirSelection>(Handle_NetworkChangePlayerCharacterAfterHeirSelection);

        messageBroker.Subscribe<PlayerCharacterChangedAfterHeirSelection>(Handle_PlayerCharacterChangedAfterHeirSelection);
        messageBroker.Subscribe<NetworkPlayerCharacterChangedAfterHeirSelection>(Handle_NetworkPlayerCharacterChangedAfterHeirSelection);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerHeirSelectionRequested>(Handle_PlayerHeirSelectionRequested);
        messageBroker.Unsubscribe<NetworkClientSelectHeir>(Handle_NetworkClientSelectHeir);

        messageBroker.Unsubscribe<HeirSelectionOver>(Handle_HeirSelectionOver);
        messageBroker.Unsubscribe<NetworkHeirSelectionOver>(Handle_NetworkHeirSelectionOver);

        messageBroker.Unsubscribe<ChangePlayerCharacterAfterHeirSelection>(Handle_ChangePlayerCharacterAfterHeirSelection);
        messageBroker.Unsubscribe<NetworkChangePlayerCharacterAfterHeirSelection>(Handle_NetworkChangePlayerCharacterAfterHeirSelection);

        messageBroker.Unsubscribe<PlayerCharacterChangedAfterHeirSelection>(Handle_PlayerCharacterChangedAfterHeirSelection);
        messageBroker.Unsubscribe<NetworkPlayerCharacterChangedAfterHeirSelection>(Handle_NetworkPlayerCharacterChangedAfterHeirSelection);
    }

    private void Handle_PlayerHeirSelectionRequested(MessagePayload<PlayerHeirSelectionRequested> obj)
    {
        var playerHero = obj.What.PlayerHero;
        if (playerHero == null || !playerHero.IsDead) return;

        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerVictimId)) return;
        if (!TryGetPeerForHero(playerVictimId, out var peer)) return;

        var heirApparents = playerHero.Clan?.GetHeirApparents();

        // Player has no remaining heirs, send to game over screen and disconnect
        if (heirApparents == null || heirApparents.Count == 0)
        {
            network.Send(peer, new NetworkClientGameOver(playerVictimId));
            return;
        }

        // Resolve heir apparent ids for client's HeirSelectionPopupVM
        var heirIdApparents = new Dictionary<string, int>();
        foreach (var heirApparent in heirApparents)
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
        if (obj.Who is not NetPeer peer) return;

        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetPlayer(peer, out var player) || player.HeroId != data.OriginalHeroId)
            {
                Logger.Warning($"Ignoring heir selection for hero {data.OriginalHeroId} from peer {peer.Id} because that peer no longer controls the hero");
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OriginalHeroId, out var originalHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.SelectedHeirId, out var selectedHeir)) return;

            if (!originalHero.IsDead ||
                originalHero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None ||
                originalHero.Clan == null ||
                selectedHeir == originalHero ||
                !originalHero.Clan.GetHeirApparents().ContainsKey(selectedHeir))
            {
                Logger.Warning($"Ignoring invalid heir {data.SelectedHeirId} for hero {data.OriginalHeroId}");
                return;
            }

            applyHeirSelectionActionInterface.ApplyByDeath(originalHero, selectedHeir);
        });
    }

    private void Handle_ChangePlayerCharacterAfterHeirSelection(MessagePayload<ChangePlayerCharacterAfterHeirSelection> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.OriginalHero, out var originalHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Heir, out var heirId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Heir.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Heir.CharacterObject, out var characterObjectId)) return;
        if (!TryGetPlayerForHero(originalHeroId, out var registeredPlayer)) return;

        objectManager.TryGetObject(registeredPlayer.MobilePartyId, out MobileParty originalParty);

        var replacementPlayerData = new Player(
            registeredPlayer.ControllerId,
            heirId,
            registeredPlayer.MobilePartyId,
            clanId,
            characterObjectId);

        if (!playerPartyRestorer.TryRestore(replacementPlayerData, out var replacementPlayer))
        {
            Logger.Error($"Could not prepare heir {heirId} as the new player for controller {registeredPlayer.ControllerId}");
            return;
        }

        if (!playerManager.ReplacePlayer(registeredPlayer, replacementPlayer))
        {
            Logger.Error($"Could not replace player registration for controller {registeredPlayer.ControllerId} after heir selection");
            return;
        }

        // Migrate CoopSession data before changed player action
        // Server updates key and sends updated version to clients
        // When ChangePlayerCharacterAction.Apply calls PlayerHeroChanged, client automatically updates data
        //coopSessionMigrator.MigratePlayerData();

        heirSelectionCampaignBehaviorInterface.OnBeforePlayerCharacterChanged(data.OriginalHero, originalParty);

        Logger.Information($"Transferred controller {registeredPlayer.ControllerId} from hero {originalHeroId} to heir {heirId}");

        messageBroker.Publish(this, new PlayerHeirSelectionCompleted(data.Heir));
        network.SendAll(new NetworkChangePlayerCharacterAfterHeirSelection(replacementPlayer, originalHeroId));

        // Only disband/destroy party if the selected heir isn't in the same party as the dead/retired player
        if (originalParty != null && replacementPlayer.MobilePartyId != registeredPlayer.MobilePartyId)
        {
            if (originalParty.IsActive)
            {
                DisbandPartyAction.StartDisband(originalParty);
            }
            else
            {
                DestroyPartyAction.Apply(null, originalParty);
            }
        }
    }

    private void Handle_NetworkChangePlayerCharacterAfterHeirSelection(MessagePayload<NetworkChangePlayerCharacterAfterHeirSelection> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            var replacementPlayer = data.Player;
            if (replacementPlayer == null) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(replacementPlayer.HeroId, out var heir)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(replacementPlayer.MobilePartyId, out var heirParty)) return;

            if (!playerManager.TryGetPlayer(replacementPlayer.ControllerId, out var registeredPlayer))
            {
                Logger.Error($"Could not find player registration for controller {replacementPlayer.ControllerId} after heir selection");
                return;
            }

            if (registeredPlayer.HeroId == replacementPlayer.HeroId) return;
            if (registeredPlayer.HeroId != data.OriginalHeroId)
            {
                Logger.Warning($"Ignoring heir registration for {replacementPlayer.ControllerId} because it expected hero {data.OriginalHeroId} but found {registeredPlayer.HeroId}");
                return;
            }

            if (!playerManager.ReplacePlayer(registeredPlayer, replacementPlayer))
            {
                Logger.Error("Could not replace player registration for controller {ControllerId} after heir selection", replacementPlayer.ControllerId);
                return;
            }

            if (!heir.IsControlledByThisInstance()) return;

            ChangePlayerCharacterAction.Apply(heir);
        });
    }

    private void Handle_PlayerCharacterChangedAfterHeirSelection(MessagePayload<PlayerCharacterChangedAfterHeirSelection> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.OldPlayer, out var oldPlayerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.NewPlayer, out var newPlayerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.NewMainParty, out var newMainPartyId)) return;

        network.SendAll(new NetworkPlayerCharacterChangedAfterHeirSelection(oldPlayerId, newPlayerId, newMainPartyId, data.IsMainPartyChanged));
    }

    private void Handle_NetworkPlayerCharacterChangedAfterHeirSelection(MessagePayload<NetworkPlayerCharacterChangedAfterHeirSelection> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OldPlayerId, out var oldPlayerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.NewPlayerId, out var newPlayerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.NewMainPartyId, out var newMainParty)) return;

            heirSelectionCampaignBehaviorInterface.OnPlayerCharacterChanged(oldPlayerHero, newPlayerHero, newMainParty, data.IsMainPartyChanged);
        });
    }

    private bool TryGetPeerForHero(string playerHeroId, out NetPeer peer)
    {
        peer = null;

        if (!TryGetPlayerForHero(playerHeroId, out var player)) return false;

        return playerManager.TryGetPeer(player.ControllerId, out peer);
    }

    private bool TryGetPlayerForHero(string playerHeroId, out Player player)
    {
        player = null;

        foreach (var candidate in playerManager.Players)
        {
            if (candidate.HeroId != playerHeroId) continue;

            player = candidate;
            return true;
        }

        Logger.Error($"Failed to get peer for player hero with id {playerHeroId} during heir selection");
        return false;
    }
}
