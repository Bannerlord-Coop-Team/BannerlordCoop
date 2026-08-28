using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.UI.Cutscenes.Messages;
using SandBox.CampaignBehaviors;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.CampaignService.Handlers;

public class GameOverState
{
    public static bool IsGameOver = false;
}

internal class GameOverHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameOverHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly HashSet<Hero> gameOverHeroes = new();

    public GameOverHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ClientGameOver>(Handle_ClientGameOver);
        messageBroker.Subscribe<NetworkClientGameOver>(Handle_NetworkClientGameOver);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientGameOver>(Handle_ClientGameOver);
        messageBroker.Unsubscribe<NetworkClientGameOver>(Handle_NetworkClientGameOver);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        GameOverState.IsGameOver = false;
    }

    private void Handle_ClientGameOver(MessagePayload<ClientGameOver> obj)
    {
        if (ModInformation.IsClient) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.PlayerHero, out var playerHeroId)) return;
        if (!gameOverHeroes.Add(obj.What.PlayerHero)) return;

        var message = new InitiateCutscenePlayerCharacterDied(obj.What.PlayerHero, obj.What.Killer, obj.What.Detail);
        messageBroker.Publish(this, message);

        network.SendAll(new NetworkClientGameOver(playerHeroId));
    }

    private void Handle_NetworkClientGameOver(MessagePayload<NetworkClientGameOver> obj)
    {
        if (ModInformation.IsServer) return;
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;

            if (playerHero != Hero.MainHero || GameOverState.IsGameOver) return;

            GameOverState.IsGameOver = true;
            messageBroker.Publish(this, new PlayerDeleteRequested(keepConnected: true));

            if (!TryGetHeirSelectionBehavior(out var heirSelectionBehavior)) return;

            if (PlayerEncounter.Current != null && (PlayerEncounter.Battle == null || !PlayerEncounter.Battle.IsFinalized))
            {
                PlayerEncounter.Finish(true);
            }

            heirSelectionBehavior.ShowGameStatistics(); // TODO: Track statistics

            if (Campaign.Current.CurrentMenuContext != null)
            {
                GameMenu.ExitToLast();
            }
        });
    }

    private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        GameOverState.IsGameOver = false;
        gameOverHeroes.Clear();
    }

    private bool TryGetHeirSelectionBehavior(out HeirSelectionCampaignBehavior heirSelectionBehavior)
    {
        heirSelectionBehavior = Campaign.Current?.GetCampaignBehavior<HeirSelectionCampaignBehavior>();
        if (heirSelectionBehavior != null) return true;

        Logger.Debug("Skipping heir selection update because the campaign behavior is unavailable.");
        return false;
    }
}
