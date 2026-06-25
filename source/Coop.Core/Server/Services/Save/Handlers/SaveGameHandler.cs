using Common.Messaging;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Alleys;
using GameInterface.Services.Heroes.Messages;
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

        CraftingPlayerData craftingPlayerData = coopSessionProvider.CoopSession.CraftingPlayerData;
        craftingPlayerData ??= new(new(), new(), new());

        WorkshopPlayerData workshopPlayerData = coopSessionProvider.CoopSession.WorkshopPlayerData;
        workshopPlayerData ??= new(new());

        AlleyPlayerData alleyPlayerData = coopSessionProvider.CoopSession.AlleyPlayerData;
        alleyPlayerData ??= new(new());

        CoopSession session = new CoopSession(saveName, playerRegistry.Players.ToArray(), craftingPlayerData, workshopPlayerData, alleyPlayerData);
        coopSessionProvider.CoopSession = session;

        saveManager.SaveCoopSession(saveName, session);
    }

    private ICoopSession savedSession;
    private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
    {
        savedSession = saveManager.LoadCoopSession(obj.What.SaveName);
        if(savedSession == null)
        {
            savedSession = new CoopSession(obj.What.SaveName, new Player[0], new CraftingPlayerData(new(), new(), new()), new WorkshopPlayerData(new()), new AlleyPlayerData(new()));
        }
        else if (savedSession.AlleyPlayerData == null)
        {
            // Saves created before AlleyPlayerData existed deserialize with a null field; give it an
            // empty store so alley management can be recorded after loading such a save.
            savedSession = new CoopSession(savedSession.UniqueGameId, savedSession.Players, savedSession.CraftingPlayerData, savedSession.WorkshopPlayerData, new AlleyPlayerData(new()));
        }
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
