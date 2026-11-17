using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Actions.TeleportHeroAction;

namespace GameInterface.Services.Companions.Interfaces;

public interface IPlayerTrackCompanionBehaviorInterface : IGameAbstraction
{
    void OnHeroTeleportationRequested(Hero hero, MobileParty party, TeleportationDetail detail);
    void AddHeroToScatteredCompanions(Hero hero);
    void SettlementEntered(MobileParty party, Settlement settlement);
    void CompanionAdded(Hero companion);
    void CompanionRemoved(Hero companion);
    void OnMobilePartyCreated(MobileParty mobileParty);
    void OnHeroJoinedParty(Hero hero, MobileParty party);
}

public class PlayerTrackCompanionBehaviorInterface : IPlayerTrackCompanionBehaviorInterface
{
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly ISessionScatteredCompanionsInterface sessionScatteredCompanionsInterface;

    public PlayerTrackCompanionBehaviorInterface(
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        ISessionScatteredCompanionsInterface sessionScatteredCompanionsInterface)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.sessionScatteredCompanionsInterface = sessionScatteredCompanionsInterface;
    }

    public void OnHeroTeleportationRequested(Hero hero, MobileParty party, TeleportationDetail detail)
    {
        if (!hero.IsPlayerCompanion || !playerManager.Contains(party)) return;

        if (detail != TeleportationDetail.DelayedTeleportToParty) return;

        if (!objectManager.TryGetIdWithLogging(hero, out var companionHeroId)) return;

        sessionScatteredCompanionsInterface.RemoveScatteredCompanion(companionHeroId);
    }

    public void AddHeroToScatteredCompanions(Hero hero)
    {
        if (!hero.IsPlayerCompanion) return;
        
        if (!objectManager.TryGetIdWithLogging(hero.CompanionOf.Leader, out var playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(hero, out var companionHeroId)) return;

        sessionScatteredCompanionsInterface.AddScatteredCompanion(playerHeroId, companionHeroId, CampaignTime.Now);
    }

    public void SettlementEntered(MobileParty party, Settlement settlement)
    {
        if (!playerManager.TryGetPlayer(party, out var player)) return;

        foreach (var companionHeroId in sessionScatteredCompanionsInterface.GetScatteredCompanions(player.HeroId))
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(companionHeroId, out var companion)) continue;

            if (companion.CurrentSettlement == settlement)
            {
                messageBroker.Publish(this, new NotifyScatteredCompanionInSettlement(party, companion));

                sessionScatteredCompanionsInterface.RemoveScatteredCompanion(companionHeroId);
            }
        }
    }

    public void CompanionAdded(Hero companion)
    {
        if (!objectManager.TryGetIdWithLogging(companion, out var companionHeroId)) return;

        sessionScatteredCompanionsInterface.RemoveScatteredCompanion(companionHeroId);
    }

    public void CompanionRemoved(Hero companion)
    {
        if (!objectManager.TryGetIdWithLogging(companion, out var companionHeroId)) return;

        sessionScatteredCompanionsInterface.RemoveScatteredCompanion(companionHeroId);
    }

    public void OnMobilePartyCreated(MobileParty mobileParty)
    {
        if (mobileParty.LeaderHero == null) return;
        if (!mobileParty.LeaderHero.IsPlayerCompanion) return;

        if (!objectManager.TryGetIdWithLogging(mobileParty.LeaderHero, out var companionHeroId)) return;

        sessionScatteredCompanionsInterface.RemoveScatteredCompanion(companionHeroId);
    }

    // A scattered companion that joins a player's party is no longer scattered, as vanilla does for the main party.
    public void OnHeroJoinedParty(Hero hero, MobileParty party)
    {
        if (!playerManager.Contains(party)) return;

        if (!objectManager.TryGetIdWithLogging(hero, out var companionHeroId)) return;

        sessionScatteredCompanionsInterface.RemoveScatteredCompanion(companionHeroId);
    }
}
