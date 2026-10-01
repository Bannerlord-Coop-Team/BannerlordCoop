using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.HeirSelection.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.UI.Cutscenes.Handlers;
using SandBox.CampaignBehaviors;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.CampaignService.Handlers;

public class GameOverState
{
    public static bool IsGameOver = false;
}

internal class GameOverHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameOverHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly PlayerDeathCutsceneHandler cutscenesHandler;
    private readonly HeirSelectionHandler heirSelectionHandler;

    public GameOverHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        PlayerDeathCutsceneHandler cutscenesHandler,
        HeirSelectionHandler heirSelectionHandler)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.cutscenesHandler = cutscenesHandler;
        this.heirSelectionHandler = heirSelectionHandler;

        messageBroker.Subscribe<NetworkClientGameOver>(Handle_NetworkClientGameOver);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkClientGameOver>(Handle_NetworkClientGameOver);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        GameOverState.IsGameOver = false;
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
            heirSelectionHandler.EndSelection();
            messageBroker.Publish(this, new PlayerDeleteRequested(keepConnected: true));

            cutscenesHandler.EnqueueDeathPresentation(() =>
            {
                if (!TryGetHeirSelectionBehavior(out var heirSelectionBehavior)) return;

                if (PlayerEncounter.Current != null && (PlayerEncounter.Battle == null || !PlayerEncounter.Battle.IsFinalized))
                {
                    PlayerEncounter.Finish(true);
                }

                if (data.ClanSurvives)
                {
                    var description = GameTexts.FindText("str_coop_succession_member_game_over");
                    if (data.AppointedLeaderId != null && objectManager.TryGetObject<Hero>(data.AppointedLeaderId, out var leader))
                        description = GameTexts.FindText("str_coop_succession_leader_game_over")
                            .SetTextVariable("HERO", leader.Name).SetTextVariable("CLAN", playerHero.Clan.Name);
                    InformationManager.ShowInquiry(new InquiryData(
                        GameTexts.FindText("str_coop_succession_game_over_title").ToString(),
                        GameTexts.FindText("str_coop_clan_experimental_warning") + "\n\n" + description, true, false,
                        GameTexts.FindText("str_continue").ToString(), string.Empty, () =>
                        {
                            var state = Game.Current.GameStateManager.CreateState<TaleWorlds.CampaignSystem.GameState.GameOverState>(TaleWorlds.CampaignSystem.GameState.GameOverState.GameOverReason.ClanDestroyed);
                            Game.Current.GameStateManager.CleanAndPushState(state);
                        }, null));
                }
                else
                {
                    heirSelectionBehavior.ShowGameStatistics(); // TODO: Track statistics
                }

                if (Campaign.Current.CurrentMenuContext != null)
                {
                    GameMenu.ExitToLast();
                }
            });
        });
    }

    private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        GameOverState.IsGameOver = false;
    }

    private bool TryGetHeirSelectionBehavior(out HeirSelectionCampaignBehavior heirSelectionBehavior)
    {
        heirSelectionBehavior = Campaign.Current?.GetCampaignBehavior<HeirSelectionCampaignBehavior>();
        if (heirSelectionBehavior != null) return true;

        Logger.Debug("Skipping heir selection update because the campaign behavior is unavailable.");
        return false;
    }
}
