using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Cutscenes.Messages;
using SandBox.CampaignBehaviors;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Engine;

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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientGameOver>(Handle_ClientGameOver);
        messageBroker.Unsubscribe<NetworkClientGameOver>(Handle_NetworkClientGameOver);
    }

    private void Handle_ClientGameOver(MessagePayload<ClientGameOver> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.PlayerHero, out var playerHeroId)) return;

        var message = new InitiateCutscenePlayerCharacterDied(obj.What.PlayerHero, obj.What.PlayerHero, obj.What.Detail);
        MessageBroker.Instance.Publish(this, message);

        network.SendAll(new NetworkClientGameOver(playerHeroId));
    }

    private void Handle_NetworkClientGameOver(MessagePayload<NetworkClientGameOver> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetHeirSelectionBehavior(out var heirSelectionBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;

            if (playerHero != Hero.MainHero) return;

            GameOverState.IsGameOver = true;

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

    private bool TryGetHeirSelectionBehavior(out HeirSelectionCampaignBehavior heirSelectionBehavior)
    {
        heirSelectionBehavior = Campaign.Current?.GetCampaignBehavior<HeirSelectionCampaignBehavior>();
        if (heirSelectionBehavior != null) return true;

        Logger.Debug("Skipping heir selection update because the campaign behavior is unavailable.");
        return false;
    }
}
