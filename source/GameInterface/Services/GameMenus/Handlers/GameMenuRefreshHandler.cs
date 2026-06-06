using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.GameMenus.Handlers;

internal class GameMenuRefreshHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<GameMenuRefreshHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public GameMenuRefreshHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<RefreshGameMenu>(Handle_RefreshGameMenu);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RefreshGameMenu>(Handle_RefreshGameMenu);
    }

    private void Handle_RefreshGameMenu(MessagePayload<RefreshGameMenu> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.TargetHeroId, out var targetHero) || targetHero != Hero.MainHero) return;

        GameMenu.SwitchToMenu(obj.What.MenuName);
    }
}