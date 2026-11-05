using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.GameMenus.Handlers;

internal class ExecuteTroopActionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ExecuteTroopActionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public ExecuteTroopActionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<MenuHeroTakenToParty>(Handle_MenuHeroTakenToParty);
        messageBroker.Subscribe<MenuTakeHeroToParty>(Handle_MenuTakeHeroToParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MenuHeroTakenToParty>(Handle_MenuHeroTakenToParty);
        messageBroker.Unsubscribe<MenuTakeHeroToParty>(Handle_MenuTakeHeroToParty);
    }

    private void Handle_MenuHeroTakenToParty(MessagePayload<MenuHeroTakenToParty> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        network.SendAll(new MenuTakeHeroToParty(heroId, mainPartyId));
    }

    private void Handle_MenuTakeHeroToParty(MessagePayload<MenuTakeHeroToParty> obj)
    {
        if (ModInformation.IsServer && !playerManager.IsSenderControllingParty(obj.Who, obj.What.MainPartyId)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;
        if (ModInformation.IsServer && !IsSenderInHerosClan(obj.Who, hero)) return;

        GameThread.RunSafe(() =>
        {
            if (hero.CurrentSettlement != null && hero.CurrentSettlement.Notables?.Contains(hero) == true)
            {
                LeaveSettlementAction.ApplyForCharacterOnly(hero);
            }
            AddHeroToPartyAction.Apply(hero, mainParty, true);
        });
    }

    // The menu only offers the player's own clan members, and never another player's hero.
    private bool IsSenderInHerosClan(object sender, Hero hero)
    {
        if (playerManager.TryGetSenderHero(sender, out var actor) && actor.Clan != null && actor.Clan == hero.Clan &&
            !hero.IsPlayerHero()) return true;

        Logger.Warning("Ignoring taking hero {HeroId} into a party from a peer outside its clan", hero.StringId);
        return false;
    }
}