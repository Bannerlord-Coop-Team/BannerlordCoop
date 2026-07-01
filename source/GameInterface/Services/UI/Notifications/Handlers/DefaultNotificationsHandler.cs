using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Notifications.Messages;
using SandBox.CampaignBehaviors;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Handlers;

internal class DefaultNotificationsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<DefaultNotificationsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;

    public DefaultNotificationsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;

        messageBroker.Subscribe<NotifyCompanionRemoved>(Handle_NotifyCompanionRemoved);
        messageBroker.Subscribe<NetworkNotifyCompanionRemoved>(Handle_NetworkNotifyCompanionRemoved);

        messageBroker.Subscribe<NotifyClanTierIncreased>(Handle_NotifyClanTierIncreased);
        messageBroker.Subscribe<NetworkNotifyClanTierIncreased>(Handle_NetworkNotifyClanTierIncreased);

        messageBroker.Subscribe<NotifyRelationChanged>(Handle_NotifyRelationChanged);
        messageBroker.Subscribe<NetworkNotifyRelationChanged>(Handle_NetworkNotifyRelationChanged);

        messageBroker.Subscribe<NotifyTroopsDeserted>(Handle_NotifyTroopsDeserted);
        messageBroker.Subscribe<NetworkNotifyTroopsDeserted>(Handle_NetworkNotifyTroopsDeserted);

        messageBroker.Subscribe<NotifyClanDestroyed>(Handle_NotifyClanDestroyed);
        messageBroker.Subscribe<NetworkNotifyClanDestroyed>(Handle_NetworkNotifyClanDestroyed);

        messageBroker.Subscribe<NotifyBuildingLevelChanged>(Handle_NotifyBuildingLevelChanged);
        messageBroker.Subscribe<NetworkNotifyBuildingLevelChanged>(Handle_NetworkNotifyBuildingLevelChanged);

        messageBroker.Subscribe<NotifyHeroTeleportation>(Handle_NotifyHeroTeleportation);
        messageBroker.Subscribe<NetworkNotifyHeroTeleportation>(Handle_NetworkNotifyHeroTeleportation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyCompanionRemoved>(Handle_NotifyCompanionRemoved);
        messageBroker.Unsubscribe<NetworkNotifyCompanionRemoved>(Handle_NetworkNotifyCompanionRemoved);

        messageBroker.Unsubscribe<NotifyClanTierIncreased>(Handle_NotifyClanTierIncreased);
        messageBroker.Unsubscribe<NetworkNotifyClanTierIncreased>(Handle_NetworkNotifyClanTierIncreased);

        messageBroker.Unsubscribe<NotifyRelationChanged>(Handle_NotifyRelationChanged);
        messageBroker.Unsubscribe<NetworkNotifyRelationChanged>(Handle_NetworkNotifyRelationChanged);

        messageBroker.Unsubscribe<NotifyTroopsDeserted>(Handle_NotifyTroopsDeserted);
        messageBroker.Unsubscribe<NetworkNotifyTroopsDeserted>(Handle_NetworkNotifyTroopsDeserted);

        messageBroker.Unsubscribe<NotifyClanDestroyed>(Handle_NotifyClanDestroyed);
        messageBroker.Unsubscribe<NetworkNotifyClanDestroyed>(Handle_NetworkNotifyClanDestroyed);

        messageBroker.Unsubscribe<NotifyBuildingLevelChanged>(Handle_NotifyBuildingLevelChanged);
        messageBroker.Unsubscribe<NetworkNotifyBuildingLevelChanged>(Handle_NetworkNotifyBuildingLevelChanged);

        messageBroker.Unsubscribe<NotifyHeroTeleportation>(Handle_NotifyHeroTeleportation);
        messageBroker.Unsubscribe<NetworkNotifyHeroTeleportation>(Handle_NetworkNotifyHeroTeleportation);
    }

    private void Handle_NotifyCompanionRemoved(MessagePayload<NotifyCompanionRemoved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkNotifyCompanionRemoved(clanId, heroId, data.Detail));
        });
    }

    private void Handle_NetworkNotifyCompanionRemoved(MessagePayload<NetworkNotifyCompanionRemoved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;

            // Only notify player(s) in the same clan the companion was removed from
            if (clan != Clan.PlayerClan) return;

            CampaignEventDispatcher.Instance.OnCompanionRemoved(hero, data.Detail);
        });
    }

    private void Handle_NotifyClanTierIncreased(MessagePayload<NotifyClanTierIncreased> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

            network.SendAll(new NetworkNotifyClanTierIncreased(clanId, data.ShouldNotify));
        });
    }

    private void Handle_NetworkNotifyClanTierIncreased(MessagePayload<NetworkNotifyClanTierIncreased> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            if (clan != Clan.PlayerClan) return;

            CampaignEventDispatcher.Instance.OnClanTierChanged(clan, data.ShouldNotify);
        });
    }

    private void Handle_NotifyRelationChanged(MessagePayload<NotifyRelationChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.EffectiveHero, out var effectiveHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(data.EffectiveHeroGainedRelationWith, out var effectiveHeroGainedRelationWithId)) return;
            if (!objectManager.TryGetIdWithLogging(data.OriginalHero, out var originalHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(data.OriginalGainedRelationWith, out var originalGainedRelationWithId)) return;

            network.SendAll(new NetworkNotifyRelationChanged(effectiveHeroId, effectiveHeroGainedRelationWithId, data.RelationChange, data.ShowNotification, data.Detail, originalHeroId, originalGainedRelationWithId));
        });
    }

    private void Handle_NetworkNotifyRelationChanged(MessagePayload<NetworkNotifyRelationChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.EffectiveHeroId, out var effectiveHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.EffectiveHeroGainedRelationWithId, out var effectiveHeroGainedRelationWith)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OriginalHeroId, out var originalHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OriginalGainedRelationWithId, out var originalGainedRelationWith)) return;

            CampaignEventDispatcher.Instance.OnHeroRelationChanged(effectiveHero, effectiveHeroGainedRelationWith, data.RelationChange, data.ShowNotification, data.Detail, originalHero, originalGainedRelationWith);
        });
    }

    private void Handle_NotifyTroopsDeserted(MessagePayload<NotifyTroopsDeserted> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MobileParty, out var mobilePartyId)) return;

            var desertedTroopsData = troopRosterInterface.PackTroopRosterData(data.DesertedTroops);

            network.SendAll(new NetworkNotifyTroopsDeserted(mobilePartyId, desertedTroopsData));
        });
    }

    private void Handle_NetworkNotifyTroopsDeserted(MessagePayload<NetworkNotifyTroopsDeserted> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

            using (new AllowedThread())
            {
                TroopRoster desertedTroops = new();
                foreach (var troop in troopRosterInterface.UnpackTroopRosterData(data.DesertedTroopsData))
                {
                    desertedTroops.Add(troop);
                }

                CampaignEventDispatcher.Instance.OnTroopsDeserted(mobileParty, desertedTroops);
            }
        });
    }

    private void Handle_NotifyClanDestroyed(MessagePayload<NotifyClanDestroyed> obj)
    {
        var data = obj.What;

        network.SendAll(new NetworkNotifyClanDestroyed(data.DestroyedClanName));
    }

    private void Handle_NetworkNotifyClanDestroyed(MessagePayload<NetworkNotifyClanDestroyed> obj)
    {
        var data = obj.What;

        // Clan may no longer be registered when this notification needs to be sent.
        // Use the destroyed clan name directly instead of trying to resolve a potentially removed Clan by network id.
        GameThread.RunSafe(() =>
        {
            TextObject textObject = new TextObject("{=PBq1FyrJ}{CLAN_NAME} clan was destroyed.", null);
            textObject.SetTextVariable("CLAN_NAME", data.DestroyedClanName);
            MBInformationManager.AddQuickInformation(textObject, 0, null, null, "");
        });
    }

    private void Handle_NotifyBuildingLevelChanged(MessagePayload<NotifyBuildingLevelChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.Town, out var townId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Building, out var buildingId)) return;

            network.SendAll(new NetworkNotifyBuildingLevelChanged(townId, buildingId, data.LevelChange));
        });
    }

    private void Handle_NetworkNotifyBuildingLevelChanged(MessagePayload<NetworkNotifyBuildingLevelChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;
            if (!objectManager.TryGetObjectWithLogging<Building>(data.BuildingId, out var building)) return;

            CampaignEventDispatcher.Instance.OnBuildingLevelChanged(town, building, data.LevelChange);
        });
    }

    private void Handle_NotifyHeroTeleportation(MessagePayload<NotifyHeroTeleportation> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            string targetSettlementId = null;
            if (data.TargetSettlement != null && !objectManager.TryGetIdWithLogging(data.TargetSettlement, out targetSettlementId)) return;

            string targetPartyId = null;
            if (data.TargetParty != null && !objectManager.TryGetIdWithLogging(data.TargetParty, out targetPartyId)) return;

            network.SendAll(new NetworkNotifyHeroTeleportation(heroId, targetSettlementId, targetPartyId, data.Detail));
        });
    }

    private void Handle_NetworkNotifyHeroTeleportation(MessagePayload<NetworkNotifyHeroTeleportation> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;

            Settlement targetSettlement = null;
            if (data.TargetSettlementId != null && !objectManager.TryGetObjectWithLogging(data.TargetSettlementId, out targetSettlement)) return;

            MobileParty targetParty = null;
            if (data.TargetPartyId != null && !objectManager.TryGetObjectWithLogging(data.TargetPartyId, out targetParty)) return;

            // Skip check for hero travelling as when client runs this check the hero state has already changed
            if (data.Detail == TeleportHeroAction.TeleportationDetail.ImmediateTeleportToSettlement && hero.Clan == Clan.PlayerClan && targetSettlement.IsFortification && targetSettlement.Town.Governor == hero)
            {
                TextObject textObject3 = new TextObject("{=btynhBAn}The new governor of {SETTLEMENT}, {HERO.NAME}, has arrived and taken up the reins of office.", null);
                textObject3.SetCharacterProperties("HERO", hero.CharacterObject, false);
                textObject3.SetTextVariable("SETTLEMENT", targetSettlement.Name);
                MBInformationManager.AddQuickInformation(textObject3, 0, null, null, "");
                return;
            }

            CampaignEventDispatcher.Instance.OnHeroTeleportationRequested(hero, targetSettlement, targetParty, data.Detail);
        });
    }

    private bool TryGetNotificationsBehavior(out DefaultNotificationsCampaignBehavior campaignBehavior)
    {
        campaignBehavior = Campaign.Current?.GetCampaignBehavior<DefaultNotificationsCampaignBehavior>();
        if (campaignBehavior != null) return true;

        Logger.Debug("Skipping notification because DefaultNotificationsCampaignBehavior is unavailable");
        return false;
    }
}