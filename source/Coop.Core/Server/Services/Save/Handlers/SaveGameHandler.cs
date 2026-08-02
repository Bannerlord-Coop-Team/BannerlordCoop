using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Save.Messages;
using Serilog;
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
    private readonly INetwork network;
    private readonly HashSet<object> activeSaveSources = new HashSet<object>();

    public SaveGameHandler(
        IMessageBroker messageBroker,
        ICoopSaveManager saveManager,
        ICoopSessionProvider coopSessionProvider,
        IPlayerManager playerRegistry,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.saveManager = saveManager;
        this.coopSessionProvider = coopSessionProvider;
        this.playerRegistry = playerRegistry;
        this.network = network;

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
            current?.InventoryPlayerData ?? empty.InventoryPlayerData);

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
            loaded?.InventoryPlayerData ?? empty.InventoryPlayerData);

        coopSessionProvider.CoopSession = savedSession;
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        // savedSession is only set by GameLoaded; hosting a NEW campaign never loads a save, so
        // there is no previous session (and no players) to restore.
        if (savedSession?.Players == null) return;

        foreach (var player in savedSession.Players)
        {
            // A save written before controller ids were made unique can carry two entries for one
            // controller; AddPlayer keeps the first. The owner heals the leftover by joining — a
            // registration naming a missing hero is dropped in ResolveCharacterState — but say so,
            // because a silently skipped registration is exactly what made this hard to diagnose.
            if (!playerRegistry.AddPlayer(player))
                Logger.Warning(
                    "Skipped saved registration for controller {ControllerId} (hero {HeroId}): " +
                    "that controller is already registered",
                    player?.ControllerId, player?.HeroId);
        }
    }
}
