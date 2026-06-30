using Common.Messaging;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Alleys;
using GameInterface.Services.Caravans;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using System.Linq;

namespace Coop.Core.Server.Services.Save.Handlers;

/// <summary>
/// Handles Coop specific saving and loading
/// </summary>
internal class SaveGameHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopSaveManager saveManager;
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IPlayerManager playerRegistry;

    public SaveGameHandler(
        IMessageBroker messageBroker,
        ICoopSaveManager saveManager,
        ICoopSessionProvider coopSessionProvider,
        IPlayerManager playerRegistry) 
    {
        this.messageBroker = messageBroker;
        this.saveManager = saveManager;
        this.coopSessionProvider = coopSessionProvider;
        this.playerRegistry = playerRegistry;

        messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);

        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    private void Handle_GameSaved(MessagePayload<GameSaved> obj)
    {
        var saveName = obj.What.SaveName;
        var current = coopSessionProvider.CoopSession;

        CoopSession session = new CoopSession(
            saveName,
            playerRegistry.Players.ToArray(),
            current?.CraftingPlayerData ?? new CraftingPlayerData(new(), new(), new()),
            current?.WorkshopPlayerData ?? new WorkshopPlayerData(new()),
            current?.CaravansPlayerData ?? new CaravansPlayerData(new(), new()),
            current?.AlleyPlayerData ?? new AlleyPlayerData(new()),
            current?.InteractionsPlayerData ?? new InteractionsPlayerData(new(), new(), new(), new()));

        coopSessionProvider.CoopSession = session;

        saveManager.SaveCoopSession(saveName, session);
    }

    private ICoopSession savedSession;
    private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
    {
        var loaded = saveManager.LoadCoopSession(obj.What.SaveName);

        savedSession = new CoopSession(
            loaded?.UniqueGameId ?? obj.What.SaveName,
            loaded?.Players ?? new Player[0],
            loaded?.CraftingPlayerData ?? new CraftingPlayerData(new(), new(), new()),
            loaded?.WorkshopPlayerData ?? new WorkshopPlayerData(new()),
            loaded?.CaravansPlayerData ?? new CaravansPlayerData(new(), new(), new()),
            loaded?.AlleyPlayerData ?? new AlleyPlayerData(new()),
            loaded?.InteractionsPlayerData ?? new InteractionsPlayerData(new(), new(), new(), new()));

        coopSessionProvider.CoopSession = savedSession;
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        if (savedSession.Players == null) return;

        foreach (var player in savedSession.Players)
        {
            playerRegistry.AddPlayer(player);
        }
    }
}
