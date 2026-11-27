using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Save.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Services.Save.Handlers;

/// <summary>
/// Handles Coop specific saving and loading
/// </summary>
internal class SaveGameHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SaveGameHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopSaveManager saveManager;
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IPlayerManager playerRegistry;
    private readonly IPlayerPartyRestorer playerPartyRestorer;
    private readonly INetwork network;
    private readonly ISavedPlayerRegistrationSelector registrationSelector;
    private readonly HashSet<object> activeSaveSources = new HashSet<object>();

    public SaveGameHandler(
        IMessageBroker messageBroker,
        ICoopSaveManager saveManager,
        ICoopSessionProvider coopSessionProvider,
        IPlayerManager playerRegistry,
        IPlayerPartyRestorer playerPartyRestorer,
        INetwork network,
        ISavedPlayerRegistrationSelector registrationSelector)
    {
        this.messageBroker = messageBroker;
        this.saveManager = saveManager;
        this.coopSessionProvider = coopSessionProvider;
        this.playerRegistry = playerRegistry;
        this.playerPartyRestorer = playerPartyRestorer;
        this.network = network;
        this.registrationSelector = registrationSelector;

        messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);
        messageBroker.Subscribe<GameSaveStateChanged>(Handle_GameSaveStateChanged);

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);
        messageBroker.Unsubscribe<GameSaveStateChanged>(Handle_GameSaveStateChanged);

        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    private void Handle_GameSaveStateChanged(MessagePayload<GameSaveStateChanged> payload)
    {
        lock (activeSaveSources)
        {
            if (payload.What.IsSaving)
            {
                if (!activeSaveSources.Add(payload.Who) || activeSaveSources.Count != 1)
                    return;
            }
            else if (!activeSaveSources.Remove(payload.Who) || activeSaveSources.Count != 0)
            {
                return;
            }

            network.SendAll(new NetworkGameSaveStateChanged(payload.What.IsSaving));
        }
    }

    private void Handle_GameSaved(MessagePayload<GameSaved> obj)
    {
        var saveName = obj.What.SaveName;
        try
        {
            WriteCoopSession(saveName);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to write the co-op session for save {SaveName}", saveName);
            messageBroker.Publish(this, new CoopSessionWritten(saveName, false));
            return;
        }

        messageBroker.Publish(this, new CoopSessionWritten(saveName, true));
    }

    private void WriteCoopSession(string saveName)
    {
        var current = coopSessionProvider.CoopSession;
        var empty = CoopSession.Empty;

        CoopSession session = new CoopSession(
            saveName,
            playerRegistry.Players.ToArray(),
            current?.CraftingPlayerData ?? empty.CraftingPlayerData,
            current?.WorkshopPlayerData ?? empty.WorkshopPlayerData,
            current?.CaravansPlayerData ?? empty.CaravansPlayerData,
            current?.AlleyPlayerData ?? empty.AlleyPlayerData,
            current?.InteractionsPlayerData ?? empty.InteractionsPlayerData,
            current?.TradePlayerData ?? empty.TradePlayerData,
            current?.InventoryPlayerData ?? empty.InventoryPlayerData,
            current?.HeroMeetingData ?? empty.HeroMeetingData,
            current?.AgingPlayerData ?? empty.AgingPlayerData,
            current?.NavalPlayerData ?? empty.NavalPlayerData,
            current?.ClanFinance ?? empty.ClanFinance,
            current?.PlayerTraitData ?? empty.PlayerTraitData,
            current?.OrderOfBattlePlayerData ?? empty.OrderOfBattlePlayerData,
            current?.StatisticsPlayerData ?? empty.StatisticsPlayerData,
            current?.IncidentsPlayerData ?? empty.IncidentsPlayerData,
            current?.ViewDataPlayerData ?? empty.ViewDataPlayerData);

        coopSessionProvider.CoopSession = session;

        saveManager.SaveCoopSession(saveName, session);
    }

    private ICoopSession savedSession;
    private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
    {
        var loaded = saveManager.LoadCoopSession(obj.What.SaveName);
        var empty = CoopSession.Empty;

        savedSession = new CoopSession(
            loaded?.UniqueGameId ?? obj.What.SaveName,
            loaded?.Players ?? empty.Players,
            loaded?.CraftingPlayerData ?? empty.CraftingPlayerData,
            loaded?.WorkshopPlayerData ?? empty.WorkshopPlayerData,
            loaded?.CaravansPlayerData ?? empty.CaravansPlayerData,
            loaded?.AlleyPlayerData ?? empty.AlleyPlayerData,
            loaded?.InteractionsPlayerData ?? empty.InteractionsPlayerData,
            loaded?.TradePlayerData ?? empty.TradePlayerData,
            loaded?.InventoryPlayerData ?? empty.InventoryPlayerData,
            loaded?.HeroMeetingData ?? empty.HeroMeetingData,
            loaded?.AgingPlayerData ?? empty.AgingPlayerData,
            loaded?.NavalPlayerData ?? empty.NavalPlayerData,
            loaded?.ClanFinance ?? empty.ClanFinance,
            loaded?.PlayerTraitData ?? empty.PlayerTraitData,
            loaded?.OrderOfBattlePlayerData ?? empty.OrderOfBattlePlayerData,
            loaded?.StatisticsPlayerData ?? empty.StatisticsPlayerData,
            loaded?.IncidentsPlayerData ?? empty.IncidentsPlayerData,
            loaded?.ViewDataPlayerData ?? empty.ViewDataPlayerData);

        coopSessionProvider.CoopSession = savedSession;
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        // savedSession is only set by GameLoaded; hosting a NEW campaign never loads a save, so
        // there is no previous session (and no players) to restore.
        if (savedSession?.Players == null) return;

        var rows = registrationSelector.Select(savedSession.Players, out var playerHeroes);

        var restored = new List<Player>();
        foreach (var player in rows)
        {
            // The restorer calls vanilla party code, and one bad row must not cost every other player
            // their registration
            Player registration;
            bool restoredGraph;
            try
            {
                restoredGraph = playerPartyRestorer.TryRestore(player, out registration);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Skipped saved registration for hero {HeroId}: restoring its party threw", player.HeroId);
                continue;
            }

            if (!restoredGraph)
            {
                Logger.Error(
                    "Skipped saved registration for controller {ControllerId} (hero {HeroId}): " +
                    "the player graph could not be restored",
                    player.ControllerId,
                    player.HeroId);
                continue;
            }

            restored.Add(registration);
        }

        foreach (var registration in restored)
        {
            // Checked once every row is restored, since restoring another row can hand this party to that row's hero
            // (vanilla ChangePartyLeader also changes a lord party's owner)
            if (!playerPartyRestorer.TryGetOwnedParty(registration, playerHeroes, out _))
            {
                Logger.Error(
                    "Skipped saved registration for hero {HeroId}: its saved party {PartyId} belongs to another hero. " +
                    SavedPlayerRegistrationSelector.CopySessionFileHint,
                    registration.HeroId,
                    registration.MobilePartyId);
                continue;
            }

            if (!playerRegistry.AddPlayer(registration))
                Logger.Warning(
                    "Skipped saved registration for hero {HeroId}: the player registry refused it",
                    registration.HeroId);
        }

        messageBroker.Publish(this, new SavedPlayerRegistrationsRestored());
    }
}
