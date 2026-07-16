using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Notifications.Messages;
using Helpers;
using SandBox.CampaignBehaviors;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
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

        messageBroker.Subscribe<NotifyAllianceStarted>(Handle_NotifyAllianceStarted);
        messageBroker.Subscribe<NetworkNotifyAllianceStarted>(Handle_NetworkNotifyAllianceStarted);

        messageBroker.Subscribe<NotifyAllianceEnded>(Handle_NotifyAllianceEnded);
        messageBroker.Subscribe<NetworkNotifyAllianceEnded>(Handle_NetworkNotifyAllianceEnded);

        messageBroker.Subscribe<NotifyCallWarToWarAgreementStarted>(Handle_NotifyCallWarToWarAgreementStarted);
        messageBroker.Subscribe<NetworkNotifyCallWarToWarAgreementStarted>(Handle_NetworkNotifyCallWarToWarAgreementStarted);

        messageBroker.Subscribe<NotifyCallWarToWarAgreementEnded>(Handle_NotifyCallWarToWarAgreementEnded);
        messageBroker.Subscribe<NetworkNotifyCallWarToWarAgreementEnded>(Handle_NetworkNotifyCallWarToWarAgreementEnded);

        messageBroker.Subscribe<NotifySettlementEntered>(Handle_NotifySettlementEntered);
        messageBroker.Subscribe<NetworkNotifySettlementEntered>(Handle_NetworkNotifySettlementEntered);

        messageBroker.Subscribe<NotifyPartyAddedToMapEvent>(Handle_NotifyPartyAddedToMapEvent);
        messageBroker.Subscribe<NetworkNotifyPartyAddedToMapEvent>(Handle_NetworkNotifyPartyAddedToMapEvent);

        messageBroker.Subscribe<NotifyCompanionRemoved>(Handle_NotifyCompanionRemoved);
        messageBroker.Subscribe<NetworkNotifyCompanionRemoved>(Handle_NetworkNotifyCompanionRemoved);

        messageBroker.Subscribe<NotifyRenownGained>(Handle_NotifyRenownGained);
        messageBroker.Subscribe<NetworkNotifyRenownGained>(Handle_NetworkNotifyRenownGained);

        messageBroker.Subscribe<NotifyHideoutSpotted>(Handle_NotifyHideoutSpotted);
        messageBroker.Subscribe<NetworkNotifyHideoutSpotted>(Handle_NetworkNotifyHideoutSpotted);

        messageBroker.Subscribe<NotifyHeroBecameFugitive>(Handle_NotifyHeroBecameFugitive);
        messageBroker.Subscribe<NetworkNotifyHeroBecameFugitive>(Handle_NetworkNotifyHeroBecameFugitive);

        messageBroker.Subscribe<NotifyPrisonerTaken>(Handle_NotifyPrisonerTaken);
        messageBroker.Subscribe<NetworkNotifyPrisonerTaken>(Handle_NetworkNotifyPrisonerTaken);

        messageBroker.Subscribe<NotifyHeroPrisonerReleased>(Handle_NotifyHeroPrisonerReleased);
        messageBroker.Subscribe<NetworkNotifyHeroPrisonerReleased>(Handle_NetworkNotifyHeroPrisonerReleased);

        messageBroker.Subscribe<NotifyBattleStarted>(Handle_NotifyBattleStarted);
        messageBroker.Subscribe<NetworkNotifyBattleStarted>(Handle_NetworkNotifyBattleStarted);

        messageBroker.Subscribe<NotifySiegeEventStarted>(Handle_NotifySiegeEventStarted);
        messageBroker.Subscribe<NetworkNotifySiegeEventStarted>(Handle_NetworkNotifySiegeEventStarted);

        messageBroker.Subscribe<NotifyClanTierIncreased>(Handle_NotifyClanTierIncreased);
        messageBroker.Subscribe<NetworkNotifyClanTierIncreased>(Handle_NetworkNotifyClanTierIncreased);

        messageBroker.Subscribe<NotifyItemsLooted>(Handle_NotifyItemsLooted);
        messageBroker.Subscribe<NetworkNotifyItemsLooted>(Handle_NetworkNotifyItemsLooted);

        messageBroker.Subscribe<NotifyRelationChanged>(Handle_NotifyRelationChanged);
        messageBroker.Subscribe<NetworkNotifyRelationChanged>(Handle_NetworkNotifyRelationChanged);

        messageBroker.Subscribe<NotifyHeroLevelledUp>(Handle_NotifyHeroLevelledUp);
        messageBroker.Subscribe<NetworkNotifyHeroLevelledUp>(Handle_NetworkNotifyHeroLevelledUp);

        messageBroker.Subscribe<NotifyHeroGainedSkill>(Handle_NotifyHeroGainedSkill);
        messageBroker.Subscribe<NetworkNotifyHeroGainedSkill>(Handle_NetworkNotifyHeroGainedSkill);

        messageBroker.Subscribe<NotifyTroopsDeserted>(Handle_NotifyTroopsDeserted);
        messageBroker.Subscribe<NetworkNotifyTroopsDeserted>(Handle_NetworkNotifyTroopsDeserted);

        messageBroker.Subscribe<NotifyClanChangedFaction>(Handle_NotifyClanChangedFaction);
        messageBroker.Subscribe<NetworkNotifyClanChangedFaction>(Handle_NetworkNotifyClanChangedFaction);

        messageBroker.Subscribe<NotifyArmyCreated>(Handle_NotifyArmyCreated);
        messageBroker.Subscribe<NetworkNotifyArmyCreated>(Handle_NetworkNotifyArmyCreated);

        messageBroker.Subscribe<NotifySiegeBombardmentHit>(Handle_NotifySiegeBombardmentHit);
        messageBroker.Subscribe<NetworkNotifySiegeBombardmentHit>(Handle_NetworkNotifySiegeBombardmentHit);

        messageBroker.Subscribe<NotifySiegeBombardmentWallHit>(Handle_NotifySiegeBombardmentWallHit);
        messageBroker.Subscribe<NetworkNotifySiegeBombardmentWallHit>(Handle_NetworkNotifySiegeBombardmentWallHit);

        messageBroker.Subscribe<NotifySiegeEngineDestroyed>(Handle_NotifySiegeEngineDestroyed);
        messageBroker.Subscribe<NetworkNotifySiegeEngineDestroyed>(Handle_NetworkNotifySiegeEngineDestroyed);

        messageBroker.Subscribe<NotifyPartyJoinedArmy>(Handle_NotifyPartyJoinedArmy);
        messageBroker.Subscribe<NetworkNotifyPartyJoinedArmy>(Handle_NetworkNotifyPartyJoinedArmy);

        messageBroker.Subscribe<NotifyPartyAttachedAnotherParty>(Handle_NotifyPartyAttachedAnotherParty);
        messageBroker.Subscribe<NetworkNotifyPartyAttachedAnotherParty>(Handle_NetworkNotifyPartyAttachedAnotherParty);

        messageBroker.Subscribe<NotifyPartyRemovedFromArmy>(Handle_NotifyPartyRemovedFromArmy);
        messageBroker.Subscribe<NetworkNotifyPartyRemovedFromArmy>(Handle_NetworkNotifyPartyRemovedFromArmy);

        messageBroker.Subscribe<ArmyDispersed>(Handle_ArmyDispersed);
        messageBroker.Subscribe<NetworkArmyDispersed>(Handle_NetworkArmyDispersed);

        messageBroker.Subscribe<NotifyHeroesMarried>(Handle_NotifyHeroesMarried);
        messageBroker.Subscribe<NetworkNotifyHeroesMarried>(Handle_NetworkNotifyHeroesMarried);

        messageBroker.Subscribe<NotifyChildConceived>(Handle_NotifyChildConceived);
        messageBroker.Subscribe<NetworkNotifyChildConceived>(Handle_NetworkNotifyChildConceived);

        messageBroker.Subscribe<NotifyGivenBirth>(Handle_NotifyGivenBirth);
        messageBroker.Subscribe<NetworkNotifyGivenBirth>(Handle_NetworkNotifyGivenBirth);

        messageBroker.Subscribe<NotifyHeroKilled>(Handle_NotifyHeroKilled);
        messageBroker.Subscribe<NetworkNotifyHeroKilled>(Handle_NetworkNotifyHeroKilled);

        messageBroker.Subscribe<HeroSharedFoodWithAnotherHero>(Handle_HeroSharedFoodWithAnotherHero);
        messageBroker.Subscribe<NetworkHeroSharedFoodWithAnotherHero>(Handle_NetworkHeroSharedFoodWithAnotherHero);

        messageBroker.Subscribe<NotifyClanDestroyed>(Handle_NotifyClanDestroyed);
        messageBroker.Subscribe<NetworkNotifyClanDestroyed>(Handle_NetworkNotifyClanDestroyed);

        messageBroker.Subscribe<NotifyHeroOrPartyGaveItem>(Handle_NotifyHeroOrPartyGaveItem);
        messageBroker.Subscribe<NetworkNotifyHeroOrPartyGaveItem>(Handle_NetworkNotifyHeroOrPartyGaveItem);

        messageBroker.Subscribe<NotifyRebellionFinished>(Handle_NotifyRebellionFinished);
        messageBroker.Subscribe<NetworkNotifyRebellionFinished>(Handle_NetworkNotifyRebellionFinished);

        messageBroker.Subscribe<NotifyTournamentFinished>(Handle_NotifyTournamentFinished);
        messageBroker.Subscribe<NetworkNotifyTournamentFinished>(Handle_NetworkNotifyTournamentFinished);

        messageBroker.Subscribe<NotifyBuildingLevelChanged>(Handle_NotifyBuildingLevelChanged);
        messageBroker.Subscribe<NetworkNotifyBuildingLevelChanged>(Handle_NetworkNotifyBuildingLevelChanged);

        messageBroker.Subscribe<NotifyHeroTeleportation>(Handle_NotifyHeroTeleportation);
        messageBroker.Subscribe<NetworkNotifyHeroTeleportation>(Handle_NetworkNotifyHeroTeleportation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyAllianceStarted>(Handle_NotifyAllianceStarted);
        messageBroker.Unsubscribe<NetworkNotifyAllianceStarted>(Handle_NetworkNotifyAllianceStarted);

        messageBroker.Unsubscribe<NotifyAllianceEnded>(Handle_NotifyAllianceEnded);
        messageBroker.Unsubscribe<NetworkNotifyAllianceEnded>(Handle_NetworkNotifyAllianceEnded);

        messageBroker.Unsubscribe<NotifyCallWarToWarAgreementStarted>(Handle_NotifyCallWarToWarAgreementStarted);
        messageBroker.Unsubscribe<NetworkNotifyCallWarToWarAgreementStarted>(Handle_NetworkNotifyCallWarToWarAgreementStarted);

        messageBroker.Unsubscribe<NotifyCallWarToWarAgreementEnded>(Handle_NotifyCallWarToWarAgreementEnded);
        messageBroker.Unsubscribe<NetworkNotifyCallWarToWarAgreementEnded>(Handle_NetworkNotifyCallWarToWarAgreementEnded);

        messageBroker.Unsubscribe<NotifySettlementEntered>(Handle_NotifySettlementEntered);
        messageBroker.Unsubscribe<NetworkNotifySettlementEntered>(Handle_NetworkNotifySettlementEntered);

        messageBroker.Unsubscribe<NotifyPartyAddedToMapEvent>(Handle_NotifyPartyAddedToMapEvent);
        messageBroker.Unsubscribe<NetworkNotifyPartyAddedToMapEvent>(Handle_NetworkNotifyPartyAddedToMapEvent);

        messageBroker.Unsubscribe<NotifyCompanionRemoved>(Handle_NotifyCompanionRemoved);
        messageBroker.Unsubscribe<NetworkNotifyCompanionRemoved>(Handle_NetworkNotifyCompanionRemoved);

        messageBroker.Unsubscribe<NotifyRenownGained>(Handle_NotifyRenownGained);
        messageBroker.Unsubscribe<NetworkNotifyRenownGained>(Handle_NetworkNotifyRenownGained);

        messageBroker.Unsubscribe<NotifyHideoutSpotted>(Handle_NotifyHideoutSpotted);
        messageBroker.Unsubscribe<NetworkNotifyHideoutSpotted>(Handle_NetworkNotifyHideoutSpotted);

        messageBroker.Unsubscribe<NotifyHeroBecameFugitive>(Handle_NotifyHeroBecameFugitive);
        messageBroker.Unsubscribe<NetworkNotifyHeroBecameFugitive>(Handle_NetworkNotifyHeroBecameFugitive);

        messageBroker.Unsubscribe<NotifyPrisonerTaken>(Handle_NotifyPrisonerTaken);
        messageBroker.Unsubscribe<NetworkNotifyPrisonerTaken>(Handle_NetworkNotifyPrisonerTaken);

        messageBroker.Unsubscribe<NotifyHeroPrisonerReleased>(Handle_NotifyHeroPrisonerReleased);
        messageBroker.Unsubscribe<NetworkNotifyHeroPrisonerReleased>(Handle_NetworkNotifyHeroPrisonerReleased);

        messageBroker.Unsubscribe<NotifyBattleStarted>(Handle_NotifyBattleStarted);
        messageBroker.Unsubscribe<NetworkNotifyBattleStarted>(Handle_NetworkNotifyBattleStarted);

        messageBroker.Unsubscribe<NotifySiegeEventStarted>(Handle_NotifySiegeEventStarted);
        messageBroker.Unsubscribe<NetworkNotifySiegeEventStarted>(Handle_NetworkNotifySiegeEventStarted);

        messageBroker.Unsubscribe<NotifyClanTierIncreased>(Handle_NotifyClanTierIncreased);
        messageBroker.Unsubscribe<NetworkNotifyClanTierIncreased>(Handle_NetworkNotifyClanTierIncreased);

        messageBroker.Unsubscribe<NotifyItemsLooted>(Handle_NotifyItemsLooted);
        messageBroker.Unsubscribe<NetworkNotifyItemsLooted>(Handle_NetworkNotifyItemsLooted);

        messageBroker.Unsubscribe<NotifyRelationChanged>(Handle_NotifyRelationChanged);
        messageBroker.Unsubscribe<NetworkNotifyRelationChanged>(Handle_NetworkNotifyRelationChanged);

        messageBroker.Unsubscribe<NotifyHeroLevelledUp>(Handle_NotifyHeroLevelledUp);
        messageBroker.Unsubscribe<NetworkNotifyHeroLevelledUp>(Handle_NetworkNotifyHeroLevelledUp);

        messageBroker.Unsubscribe<NotifyHeroGainedSkill>(Handle_NotifyHeroGainedSkill);
        messageBroker.Unsubscribe<NetworkNotifyHeroGainedSkill>(Handle_NetworkNotifyHeroGainedSkill);

        messageBroker.Unsubscribe<NotifyTroopsDeserted>(Handle_NotifyTroopsDeserted);
        messageBroker.Unsubscribe<NetworkNotifyTroopsDeserted>(Handle_NetworkNotifyTroopsDeserted);

        messageBroker.Unsubscribe<NotifyClanChangedFaction>(Handle_NotifyClanChangedFaction);
        messageBroker.Unsubscribe<NetworkNotifyClanChangedFaction>(Handle_NetworkNotifyClanChangedFaction);

        messageBroker.Unsubscribe<NotifyArmyCreated>(Handle_NotifyArmyCreated);
        messageBroker.Unsubscribe<NetworkNotifyArmyCreated>(Handle_NetworkNotifyArmyCreated);

        messageBroker.Unsubscribe<NotifySiegeBombardmentHit>(Handle_NotifySiegeBombardmentHit);
        messageBroker.Unsubscribe<NetworkNotifySiegeBombardmentHit>(Handle_NetworkNotifySiegeBombardmentHit);

        messageBroker.Unsubscribe<NotifySiegeBombardmentWallHit>(Handle_NotifySiegeBombardmentWallHit);
        messageBroker.Unsubscribe<NetworkNotifySiegeBombardmentWallHit>(Handle_NetworkNotifySiegeBombardmentWallHit);

        messageBroker.Unsubscribe<NotifySiegeEngineDestroyed>(Handle_NotifySiegeEngineDestroyed);
        messageBroker.Unsubscribe<NetworkNotifySiegeEngineDestroyed>(Handle_NetworkNotifySiegeEngineDestroyed);

        messageBroker.Unsubscribe<NotifyPartyJoinedArmy>(Handle_NotifyPartyJoinedArmy);
        messageBroker.Unsubscribe<NetworkNotifyPartyJoinedArmy>(Handle_NetworkNotifyPartyJoinedArmy);

        messageBroker.Unsubscribe<NotifyPartyAttachedAnotherParty>(Handle_NotifyPartyAttachedAnotherParty);
        messageBroker.Unsubscribe<NetworkNotifyPartyAttachedAnotherParty>(Handle_NetworkNotifyPartyAttachedAnotherParty);

        messageBroker.Unsubscribe<NotifyPartyRemovedFromArmy>(Handle_NotifyPartyRemovedFromArmy);
        messageBroker.Unsubscribe<NetworkNotifyPartyRemovedFromArmy>(Handle_NetworkNotifyPartyRemovedFromArmy);

        messageBroker.Unsubscribe<ArmyDispersed>(Handle_ArmyDispersed);
        messageBroker.Unsubscribe<NetworkArmyDispersed>(Handle_NetworkArmyDispersed);

        messageBroker.Unsubscribe<NotifyHeroesMarried>(Handle_NotifyHeroesMarried);
        messageBroker.Unsubscribe<NetworkNotifyHeroesMarried>(Handle_NetworkNotifyHeroesMarried);

        messageBroker.Unsubscribe<NotifyChildConceived>(Handle_NotifyChildConceived);
        messageBroker.Unsubscribe<NetworkNotifyChildConceived>(Handle_NetworkNotifyChildConceived);

        messageBroker.Unsubscribe<NotifyGivenBirth>(Handle_NotifyGivenBirth);
        messageBroker.Unsubscribe<NetworkNotifyGivenBirth>(Handle_NetworkNotifyGivenBirth);

        messageBroker.Unsubscribe<NotifyHeroKilled>(Handle_NotifyHeroKilled);
        messageBroker.Unsubscribe<NetworkNotifyHeroKilled>(Handle_NetworkNotifyHeroKilled);

        messageBroker.Unsubscribe<HeroSharedFoodWithAnotherHero>(Handle_HeroSharedFoodWithAnotherHero);
        messageBroker.Unsubscribe<NetworkHeroSharedFoodWithAnotherHero>(Handle_NetworkHeroSharedFoodWithAnotherHero);

        messageBroker.Unsubscribe<NotifyClanDestroyed>(Handle_NotifyClanDestroyed);
        messageBroker.Unsubscribe<NetworkNotifyClanDestroyed>(Handle_NetworkNotifyClanDestroyed);

        messageBroker.Unsubscribe<NotifyHeroOrPartyGaveItem>(Handle_NotifyHeroOrPartyGaveItem);
        messageBroker.Unsubscribe<NetworkNotifyHeroOrPartyGaveItem>(Handle_NetworkNotifyHeroOrPartyGaveItem);

        messageBroker.Unsubscribe<NotifyRebellionFinished>(Handle_NotifyRebellionFinished);
        messageBroker.Unsubscribe<NetworkNotifyRebellionFinished>(Handle_NetworkNotifyRebellionFinished);

        messageBroker.Unsubscribe<NotifyTournamentFinished>(Handle_NotifyTournamentFinished);
        messageBroker.Unsubscribe<NetworkNotifyTournamentFinished>(Handle_NetworkNotifyTournamentFinished);

        messageBroker.Unsubscribe<NotifyBuildingLevelChanged>(Handle_NotifyBuildingLevelChanged);
        messageBroker.Unsubscribe<NetworkNotifyBuildingLevelChanged>(Handle_NetworkNotifyBuildingLevelChanged);

        messageBroker.Unsubscribe<NotifyHeroTeleportation>(Handle_NotifyHeroTeleportation);
        messageBroker.Unsubscribe<NetworkNotifyHeroTeleportation>(Handle_NetworkNotifyHeroTeleportation);
    }

    private void Handle_NotifyAllianceStarted(MessagePayload<NotifyAllianceStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Kingdom1, out var kingdom1Id)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Kingdom2, out var kingdom2Id)) return;

            network.SendAll(new NetworkNotifyAllianceStarted(kingdom1Id, kingdom2Id));
        });
    }

    private void Handle_NetworkNotifyAllianceStarted(MessagePayload<NetworkNotifyAllianceStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom1Id, out var kingdom1)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom2Id, out var kingdom2)) return;

            notificationsBehavior.OnAllianceStarted(kingdom1, kingdom2);
        });
    }

    private void Handle_NotifyAllianceEnded(MessagePayload<NotifyAllianceEnded> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Kingdom1, out var kingdom1Id)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Kingdom2, out var kingdom2Id)) return;

            network.SendAll(new NetworkNotifyAllianceEnded(kingdom1Id, kingdom2Id));
        });
    }

    private void Handle_NetworkNotifyAllianceEnded(MessagePayload<NetworkNotifyAllianceEnded> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom1Id, out var kingdom1)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom2Id, out var kingdom2)) return;

            notificationsBehavior.OnAllianceEnded(kingdom1, kingdom2);
        });
    }

    private void Handle_NotifyCallWarToWarAgreementStarted(MessagePayload<NotifyCallWarToWarAgreementStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.CallingKingdom, out var callingKingdomId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.CalledKingdom, out var calledKingdomId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.KingdomToCallToWarAgainst, out var kingdomToCallToWarAgainst)) return;

            network.SendAll(new NetworkNotifyCallWarToWarAgreementStarted(callingKingdomId, calledKingdomId, kingdomToCallToWarAgainst));
        });
    }

    private void Handle_NetworkNotifyCallWarToWarAgreementStarted(MessagePayload<NetworkNotifyCallWarToWarAgreementStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CallingKingdomId, out var callingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CalledKingdomId, out var calledKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.KingdomToCallToWarAgainstId, out var kingdomToCallToWarAgainst)) return;

            notificationsBehavior.OnCallToWarAgreementStarted(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
        });
    }

    private void Handle_NotifyCallWarToWarAgreementEnded(MessagePayload<NotifyCallWarToWarAgreementEnded> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.CallingKingdom, out var callingKingdomId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.CalledKingdom, out var calledKingdomId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.KingdomToCallToWarAgainst, out var kingdomToCallToWarAgainst)) return;

            network.SendAll(new NetworkNotifyCallWarToWarAgreementEnded(callingKingdomId, calledKingdomId, kingdomToCallToWarAgainst));
        });
    }

    private void Handle_NetworkNotifyCallWarToWarAgreementEnded(MessagePayload<NetworkNotifyCallWarToWarAgreementEnded> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CallingKingdomId, out var callingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CalledKingdomId, out var calledKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.KingdomToCallToWarAgainstId, out var kingdomToCallToWarAgainst)) return;

            notificationsBehavior.OnCallToWarAgreementEnded(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
        });
    }

    private void Handle_NotifySettlementEntered(MessagePayload<NotifySettlementEntered> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Settlement, out var settlementId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

            network.SendAll(new NetworkNotifySettlementEntered(mobilePartyId, settlementId, heroId));
        });
    }

    private void Handle_NetworkNotifySettlementEntered(MessagePayload<NetworkNotifySettlementEntered> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            notificationsBehavior.OnSettlementEntered(mobileParty, settlement, hero);
        });
    }

    private void Handle_NotifyPartyAddedToMapEvent(MessagePayload<NotifyPartyAddedToMapEvent> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.InvolvedParty, out var involvedPartyId)) return;

            network.SendAll(new NetworkNotifyPartyAddedToMapEvent(involvedPartyId));
        });
    }

    private void Handle_NetworkNotifyPartyAddedToMapEvent(MessagePayload<NetworkNotifyPartyAddedToMapEvent> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.InvolvedPartyId, out var involvedParty)) return;

            notificationsBehavior.OnPartyAddedToMapEvent(involvedParty);
        });
    }

    private void Handle_NotifyCompanionRemoved(MessagePayload<NotifyCompanionRemoved> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

            network.SendAll(new NetworkNotifyCompanionRemoved(clanId, heroId, obj.What.Detail));
        });
    }

    private void Handle_NetworkNotifyCompanionRemoved(MessagePayload<NetworkNotifyCompanionRemoved> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            if (clan != Clan.PlayerClan) return;

            notificationsBehavior.OnCompanionRemoved(hero, obj.What.Detail);
        });
    }

    private void Handle_NotifyRenownGained(MessagePayload<NotifyRenownGained> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

            network.SendAll(new NetworkNotifyRenownGained(heroId, obj.What.GainedRenown, obj.What.DoNotNotifyPlayer));
        });
    }

    private void Handle_NetworkNotifyRenownGained(MessagePayload<NetworkNotifyRenownGained> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            notificationsBehavior.OnRenownGained(hero, obj.What.GainedRenown, obj.What.DoNotNotifyPlayer);
        });
    }

    private void Handle_NotifyHideoutSpotted(MessagePayload<NotifyHideoutSpotted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.SpottingParty, out var spottingPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.HideoutParty, out var hideoutPartyId)) return;

            network.SendAll(new NetworkNotifyHideoutSpotted(spottingPartyId, hideoutPartyId));
        });
    }

    private void Handle_NetworkNotifyHideoutSpotted(MessagePayload<NetworkNotifyHideoutSpotted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.SpottingPartyId, out var spottingParty)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.HideoutPartyId, out var hideoutParty)) return;

            notificationsBehavior.OnHideoutSpotted(spottingParty, hideoutParty);
        });
    }

    private void Handle_NotifyHeroBecameFugitive(MessagePayload<NotifyHeroBecameFugitive> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

            network.SendAll(new NetworkNotifyHeroBecameFugitive(heroId, obj.What.ShowNotification));
        });
    }

    private void Handle_NetworkNotifyHeroBecameFugitive(MessagePayload<NetworkNotifyHeroBecameFugitive> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            notificationsBehavior.OnHeroBecameFugitive(hero, obj.What.ShowNotification);
        });
    }

    private void Handle_NotifyPrisonerTaken(MessagePayload<NotifyPrisonerTaken> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Capturer, out var capturerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Prisoner, out var prisonerId)) return;

            network.SendAll(new NetworkNotifyPrisonerTaken(capturerId, prisonerId));
        });
    }

    private void Handle_NetworkNotifyPrisonerTaken(MessagePayload<NetworkNotifyPrisonerTaken> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.CapturerId, out var capturer)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.PrisonerId, out var prisoner)) return;

            notificationsBehavior.OnPrisonerTaken(capturer, prisoner);
        });
    }

    private void Handle_NotifyHeroPrisonerReleased(MessagePayload<NotifyHeroPrisonerReleased> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Party, out var partyId)) return;

            // Unsure how to send factions over the network
            string factionStringId = obj.What.CapturerFaction.StringId;

            network.SendAll(new NetworkNotifyHeroPrisonerReleased(heroId, partyId, factionStringId, obj.What.Detail, obj.What.ShowNotification));
        });
    }

    private void Handle_NetworkNotifyHeroPrisonerReleased(MessagePayload<NetworkNotifyHeroPrisonerReleased> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.PartyId, out var party)) return;

            IFaction capturerFaction = null;
            foreach (var faction in Campaign.Current.Factions)
            {
                if (faction.StringId == obj.What.FactionId)
                {
                    capturerFaction = faction;
                    break;
                }
            }

            notificationsBehavior.OnHeroPrisonerReleased(hero, party, capturerFaction, obj.What.Detail, obj.What.ShowNotification);
        });
    }

    private void Handle_NotifyBattleStarted(MessagePayload<NotifyBattleStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.AttackerParty, out var attackerPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.DefenderParty, out var defenderPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Subject, out var settlementId)) return;

            network.SendAll(new NetworkNotifyBattleStarted(attackerPartyId, defenderPartyId, settlementId, obj.What.ShowNotification));
        });
    }

    private void Handle_NetworkNotifyBattleStarted(MessagePayload<NetworkNotifyBattleStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.DefenderPartyId, out var defenderParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var subject)) return;

            notificationsBehavior.OnBattleStarted(attackerParty, defenderParty, subject, obj.What.ShowNotification);
        });
    }

    private void Handle_NotifySiegeEventStarted(MessagePayload<NotifySiegeEventStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.SiegeEvent, out var siegeEventId)) return;

            network.SendAll(new NetworkNotifySiegeEventStarted(siegeEventId));
        });
    }

    private void Handle_NetworkNotifySiegeEventStarted(MessagePayload<NetworkNotifySiegeEventStarted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<SiegeEvent>(obj.What.SiegeEventId, out var siegeEvent)) return;

            notificationsBehavior.OnSiegeEventStarted(siegeEvent);
        });
    }

    private void Handle_NotifyClanTierIncreased(MessagePayload<NotifyClanTierIncreased> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;

            network.SendAll(new NetworkNotifyClanTierIncreased(clanId, obj.What.ShouldNotify));
        });
    }

    private void Handle_NetworkNotifyClanTierIncreased(MessagePayload<NetworkNotifyClanTierIncreased> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;

            notificationsBehavior.OnClanTierIncreased(clan, obj.What.ShouldNotify);
        });
    }

    private void Handle_NotifyItemsLooted(MessagePayload<NotifyItemsLooted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyItemsLooted(mobilePartyId, obj.What.ItemRosterData));
        });
    }

    private void Handle_NetworkNotifyItemsLooted(MessagePayload<NetworkNotifyItemsLooted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            using (new AllowedThread())
            {
                ItemRoster lootedItems = new();
                lootedItems.Add(obj.What.ItemRosterData);

                notificationsBehavior.OnItemsLooted(mobileParty, lootedItems);
            }
        });
    }

    private void Handle_NotifyRelationChanged(MessagePayload<NotifyRelationChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.EffectiveHero, out var effectiveHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.EffectiveHeroGainedRelationWith, out var effectiveHeroGainedRelationWithId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.OriginalHero, out var originalHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.OriginalGainedRelationWith, out var originalGainedRelationWithId)) return;

            network.SendAll(new NetworkNotifyRelationChanged(effectiveHeroId, effectiveHeroGainedRelationWithId, obj.What.RelationChange, obj.What.ShowNotification, obj.What.Detail, originalHeroId, originalGainedRelationWithId));
        });
    }

    private void Handle_NetworkNotifyRelationChanged(MessagePayload<NetworkNotifyRelationChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.EffectiveHeroId, out var effectiveHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.EffectiveHeroGainedRelationWithId, out var effectiveHeroGainedRelationWith)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OriginalHeroId, out var originalHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OriginalGainedRelationWithId, out var originalGainedRelationWith)) return;

            notificationsBehavior.OnRelationChanged(effectiveHero, effectiveHeroGainedRelationWith, obj.What.RelationChange, obj.What.ShowNotification, obj.What.Detail, originalHero, originalGainedRelationWith);
        });
    }

    private void Handle_NotifyHeroLevelledUp(MessagePayload<NotifyHeroLevelledUp> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

            network.SendAll(new NetworkNotifyHeroLevelledUp(heroId, obj.What.ShouldNotify));
        });
    }

    private void Handle_NetworkNotifyHeroLevelledUp(MessagePayload<NetworkNotifyHeroLevelledUp> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!TryGetViewDataTrackerBehavior(out var viewDataTrackerBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            notificationsBehavior.OnHeroLevelledUp(hero, obj.What.ShouldNotify);
            viewDataTrackerBehavior.OnHeroLevelledUp(hero, obj.What.ShouldNotify);
        });
    }

    private void Handle_NotifyHeroGainedSkill(MessagePayload<NotifyHeroGainedSkill> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Skill, out var skillObjectId)) return;

            network.SendAll(new NetworkNotifyHeroGainedSkill(heroId, skillObjectId, obj.What.Change, obj.What.ShouldNotify));
        });
    }

    private void Handle_NetworkNotifyHeroGainedSkill(MessagePayload<NetworkNotifyHeroGainedSkill> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!TryGetViewDataTrackerBehavior(out var viewDataTrackerBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<SkillObject>(obj.What.SkillObjectId, out var skillObject)) return;

            notificationsBehavior.OnHeroGainedSkill(hero, skillObject, obj.What.Change, obj.What.ShouldNotify);
            viewDataTrackerBehavior.OnHeroGainedSkill(hero, skillObject, obj.What.Change, obj.What.ShouldNotify);
        });
    }

    private void Handle_NotifyTroopsDeserted(MessagePayload<NotifyTroopsDeserted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            var desertedTroopsData = troopRosterInterface.PackTroopRosterData(obj.What.DesertedTroops);

            network.SendAll(new NetworkNotifyTroopsDeserted(mobilePartyId, desertedTroopsData));
        });
    }

    private void Handle_NetworkNotifyTroopsDeserted(MessagePayload<NetworkNotifyTroopsDeserted> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            using (new AllowedThread())
            {
                TroopRoster desertedTroops = new();
                foreach (var troop in troopRosterInterface.UnpackTroopRosterData(obj.What.DesertedTroopsData))
                {
                    desertedTroops.Add(troop);
                }

                notificationsBehavior.OnTroopsDeserted(mobileParty, desertedTroops);
            }
        });
    }

    private void Handle_NotifyClanChangedFaction(MessagePayload<NotifyClanChangedFaction> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.OldKingdom, out var oldKingdomId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.NewKingdom, out var newKingdomId)) return;

            network.SendAll(new NetworkNotifyClanChangedFaction(clanId, oldKingdomId, newKingdomId, obj.What.Detail, obj.What.ShowNotification));
        });
    }

    private void Handle_NetworkNotifyClanChangedFaction(MessagePayload<NetworkNotifyClanChangedFaction> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.OldKingdomId, out var oldKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.NewKingdomId, out var newKingdom)) return;

            notificationsBehavior.OnClanChangedFaction(clan, oldKingdom, newKingdom, obj.What.Detail, obj.What.ShowNotification);
        });
    }

    private void Handle_NotifyArmyCreated(MessagePayload<NotifyArmyCreated> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;
            string aiBehaviorObjectId = null;
            if (obj.What.AiBehaviorObject != null)
            {
                if (!objectManager.TryGetIdWithLogging(obj.What.AiBehaviorObject, out aiBehaviorObjectId)) return;
            }
            network.SendAll(new NetworkNotifyArmyCreated(armyId, aiBehaviorObjectId));
        });
    }

    private void Handle_NetworkNotifyArmyCreated(MessagePayload<NetworkNotifyArmyCreated> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Army>(obj.What.ArmyId, out var army)) return;
            IMapPoint aiBehaviorObject = null;
            if (obj.What.AiBehaviorObjectId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<IMapPoint>(obj.What.AiBehaviorObjectId, out aiBehaviorObject)) return;
            }
            OnArmyCreated(army, aiBehaviorObject);
        });
    }
    private void OnArmyCreated(Army army, IMapPoint aiBehaviorObject)
    {
        if ((army.Kingdom == MobileParty.MainParty.MapFaction && MobileParty.MainParty.Army == null))
        {
            TextObject textObject = new TextObject("{=VEHPTzhO}{LEADER.NAME} is gathering an army near {SETTLEMENT}.");
            string settlementName = army.AiBehaviorObject?.Name?.ToString();
            if (string.IsNullOrEmpty(settlementName))
            {
                Settlement fallbackSettlement = SettlementHelper.FindNearestSettlementToPoint(army.LeaderParty.Position, null);
                settlementName = fallbackSettlement?.Name?.ToString() ?? string.Empty;
            }
            textObject.SetTextVariable("SETTLEMENT", settlementName);
            StringHelpers.SetCharacterProperties("LEADER", army.LeaderParty.LeaderHero.CharacterObject, textObject);
            MBInformationManager.AddQuickInformation(textObject, 0, army.LeaderParty.LeaderHero.CharacterObject);
        }
    }
    private void Handle_NotifySiegeBombardmentHit(MessagePayload<NotifySiegeBombardmentHit> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.BesiegerParty, out var besiegerPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.BesiegedSettlement, out var besiegedSettlementId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Weapon, out var weaponId)) return;

            network.SendAll(new NetworkNotifySiegeBombardmentHit(besiegerPartyId, besiegedSettlementId, obj.What.Side, weaponId, obj.What.Target));
        });
    }

    private void Handle_NetworkNotifySiegeBombardmentHit(MessagePayload<NetworkNotifySiegeBombardmentHit> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.BesiegerPartyId, out var besiegerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.BesiegedSettlementId, out var besiegedSettlement)) return;
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineType>(obj.What.WeaponId, out var weapon)) return;

            notificationsBehavior.OnSiegeBombardmentHit(besiegerParty, besiegedSettlement, obj.What.Side, weapon, obj.What.Target);
        });
    }

    private void Handle_NotifySiegeBombardmentWallHit(MessagePayload<NotifySiegeBombardmentWallHit> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.BesiegerParty, out var besiegerPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.BesiegedSettlement, out var besiegedSettlementId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Weapon, out var weaponId)) return;

            network.SendAll(new NetworkNotifySiegeBombardmentWallHit(besiegerPartyId, besiegedSettlementId, obj.What.Side, weaponId, obj.What.IsWallCracked));
        });
    }

    private void Handle_NetworkNotifySiegeBombardmentWallHit(MessagePayload<NetworkNotifySiegeBombardmentWallHit> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.BesiegerPartyId, out var besiegerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.BesiegedSettlementId, out var besiegedSettlement)) return;
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineType>(obj.What.WeaponId, out var weapon)) return;

            notificationsBehavior.OnSiegeBombardmentWallHit(besiegerParty, besiegedSettlement, obj.What.Side, weapon, obj.What.IsWallCracked);
        });
    }

    private void Handle_NotifySiegeEngineDestroyed(MessagePayload<NotifySiegeEngineDestroyed> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.BesiegerParty, out var besiegerPartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.BesiegedSettlement, out var besiegedSettlementId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.DestroyedEngine, out var destroyedEngineId)) return;

            network.SendAll(new NetworkNotifySiegeEngineDestroyed(besiegerPartyId, besiegedSettlementId, obj.What.Side, destroyedEngineId));
        });
    }

    private void Handle_NetworkNotifySiegeEngineDestroyed(MessagePayload<NetworkNotifySiegeEngineDestroyed> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.BesiegerPartyId, out var besiegerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.BesiegedSettlementId, out var besiegedSettlement)) return;
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineType>(obj.What.DestroyedEngineId, out var destroyedEngine)) return;

            notificationsBehavior.OnSiegeEngineDestroyed(besiegerParty, besiegedSettlement, obj.What.Side, destroyedEngine);
        });
    }

    private void Handle_NotifyPartyJoinedArmy(MessagePayload<NotifyPartyJoinedArmy> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyPartyJoinedArmy(mobilePartyId));
        });
    }

    private void Handle_NetworkNotifyPartyJoinedArmy(MessagePayload<NetworkNotifyPartyJoinedArmy> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            notificationsBehavior.OnPartyJoinedArmy(mobileParty);
        });
    }

    private void Handle_NotifyPartyAttachedAnotherParty(MessagePayload<NotifyPartyAttachedAnotherParty> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyPartyAttachedAnotherParty(mobilePartyId));
        });
    }

    private void Handle_NetworkNotifyPartyAttachedAnotherParty(MessagePayload<NetworkNotifyPartyAttachedAnotherParty> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            notificationsBehavior.OnPartyAttachedAnotherParty(mobileParty);
        });
    }

    private void Handle_NotifyPartyRemovedFromArmy(MessagePayload<NotifyPartyRemovedFromArmy> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;

            network.SendAll(new NetworkNotifyPartyRemovedFromArmy(mobilePartyId, armyId));
        });
    }

    private void Handle_NetworkNotifyPartyRemovedFromArmy(MessagePayload<NetworkNotifyPartyRemovedFromArmy> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Army>(obj.What.ArmyId, out var army)) return;

            OnPartyRemovedFromArmy(notificationsBehavior, mobileParty, army);
        });
    }
    private void OnPartyRemovedFromArmy(DefaultNotificationsCampaignBehavior __instance, MobileParty party, Army army)
    {
        if (army == MobileParty.MainParty.Army)
        {
            TextObject textObject = new TextObject("{=ApG1xg7O}{PARTY_NAME} has left {ARMY_NAME}.", null);
            textObject.SetTextVariable("PARTY_NAME", party.Name);
            textObject.SetTextVariable("ARMY_NAME", party.Army?.Name);
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));
        }
        if (party == MobileParty.MainParty)
        {
            __instance.CheckFoodNotifications();
        }
    }

    private void Handle_ArmyDispersed(MessagePayload<ArmyDispersed> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;

            network.SendAll(new NetworkArmyDispersed(armyId, obj.What.Reason, obj.What.IsPlayersArmy));
        });
    }

    private void Handle_NetworkArmyDispersed(MessagePayload<NetworkArmyDispersed> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Army>(obj.What.ArmyId, out var army)) return;

            notificationsBehavior.OnArmyDispersed(army, obj.What.Reason, obj.What.IsPlayersArmy);
        });
    }

    private void Handle_NotifyHeroesMarried(MessagePayload<NotifyHeroesMarried> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.FirstHero, out var firstHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.SecondHero, out var secondHeroId)) return;

            network.SendAll(new NetworkNotifyHeroesMarried(firstHeroId, secondHeroId, obj.What.ShowNotification));
        });
    }

    private void Handle_NetworkNotifyHeroesMarried(MessagePayload<NetworkNotifyHeroesMarried> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.FirstHeroId, out var firstHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.SecondHeroId, out var secondHero)) return;

            notificationsBehavior.OnHeroesMarried(firstHero, secondHero, obj.What.ShowNotification);
        });
    }

    private void Handle_NotifyChildConceived(MessagePayload<NotifyChildConceived> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Mother, out var motherId)) return;

            network.SendAll(new NetworkNotifyChildConceived(motherId));
        });
    }

    private void Handle_NetworkNotifyChildConceived(MessagePayload<NetworkNotifyChildConceived> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MotherId, out var mother)) return;

            notificationsBehavior.OnChildConceived(mother);
        });
    }

    private void Handle_NotifyGivenBirth(MessagePayload<NotifyGivenBirth> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Mother, out var motherId)) return;

            var aliveOffspringsIds = new List<string>();
            foreach (var aliveOffspring in obj.What.AliveOffsprings)
            {
                if (!objectManager.TryGetIdWithLogging(aliveOffspring, out var aliveOffspringId)) continue;

                aliveOffspringsIds.Add(aliveOffspringId);
            }

            network.SendAll(new NetworkNotifyGivenBirth(motherId, aliveOffspringsIds, obj.What.StillbornCount));
        });
    }

    private void Handle_NetworkNotifyGivenBirth(MessagePayload<NetworkNotifyGivenBirth> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MotherId, out var mother)) return;

            var aliveOffsprings = new List<Hero>();
            foreach (var aliveOffspringId in obj.What.AliveOffspringsIds)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(aliveOffspringId, out var aliveOffspring)) return;

                aliveOffsprings.Add(aliveOffspring);
            }

            notificationsBehavior.OnGivenBirth(mother, aliveOffsprings, obj.What.StillbornCount);
        });
    }

    private void Handle_NotifyHeroKilled(MessagePayload<NotifyHeroKilled> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.VictimHero, out var victimHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Killer, out var killerId)) return;

            network.SendAll(new NetworkNotifyHeroKilled(victimHeroId, killerId, obj.What.Detail, obj.What.ShowNotification));
        });
    }

    private void Handle_NetworkNotifyHeroKilled(MessagePayload<NetworkNotifyHeroKilled> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.VictimHeroId, out var victimHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.KillerId, out var killer)) return;

            notificationsBehavior.OnHeroKilled(victimHero, killer, obj.What.Detail, obj.What.ShowNotification);
        });
    }

    private void Handle_HeroSharedFoodWithAnotherHero(MessagePayload<HeroSharedFoodWithAnotherHero> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.SupporterHero, out var supporterHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.SupportedHero, out var supportedHeroId)) return;

            network.SendAll(new NetworkHeroSharedFoodWithAnotherHero(supporterHeroId, supportedHeroId, obj.What.Influence));
        });
    }

    private void Handle_NetworkHeroSharedFoodWithAnotherHero(MessagePayload<NetworkHeroSharedFoodWithAnotherHero> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.SupporterHeroId, out var supporterHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.SupportedHeroId, out var supportedHero)) return;

            notificationsBehavior.OnHeroSharedFoodWithAnotherHero(supporterHero, supportedHero, obj.What.Influence);
        });
    }

    private void Handle_NotifyClanDestroyed(MessagePayload<NotifyClanDestroyed> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.DestroyedClan, out var destroyedClanId)) return;

            network.SendAll(new NetworkNotifyClanDestroyed(destroyedClanId));
        });
    }

    private void Handle_NetworkNotifyClanDestroyed(MessagePayload<NetworkNotifyClanDestroyed> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.DestroyedClanId, out var destroyedClan)) return;

            notificationsBehavior.OnClanDestroyed(destroyedClan);
        });
    }

    private void Handle_NotifyHeroOrPartyGaveItem(MessagePayload<NotifyHeroOrPartyGaveItem> obj)
    {
        GameThread.RunSafe(() =>
        {
            string giverHeroId = null;
            if (obj.What.Giver.Item1 != null && !objectManager.TryGetIdWithLogging(obj.What.Giver.Item1, out giverHeroId)) return;

            string giverPartyId = null;
            if (obj.What.Giver.Item2 != null && !objectManager.TryGetIdWithLogging(obj.What.Giver.Item2, out giverPartyId)) return;

            string receiverHeroId = null;
            if (obj.What.Receiver.Item1 != null && !objectManager.TryGetIdWithLogging(obj.What.Receiver.Item1, out receiverHeroId)) return;

            string receiverPartyId = null;
            if (obj.What.Receiver.Item2 != null && !objectManager.TryGetIdWithLogging(obj.What.Receiver.Item2, out receiverPartyId)) return;

            network.SendAll(new NetworkNotifyHeroOrPartyGaveItem((giverHeroId, giverPartyId), (receiverHeroId, receiverPartyId), obj.What.ItemRosterElement, obj.What.ShowNotification));
        });
    }

    private void Handle_NetworkNotifyHeroOrPartyGaveItem(MessagePayload<NetworkNotifyHeroOrPartyGaveItem> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;

            Hero giverHero = null;
            if (obj.What.GiverIds.Item1 != null && !objectManager.TryGetObjectWithLogging(obj.What.GiverIds.Item1, out giverHero)) return;

            PartyBase giverParty = null;
            if (obj.What.GiverIds.Item2 != null && !objectManager.TryGetObjectWithLogging(obj.What.GiverIds.Item2, out giverParty)) return;

            Hero receiverHero = null;
            if (obj.What.ReceiverIds.Item1 != null && !objectManager.TryGetObjectWithLogging(obj.What.ReceiverIds.Item1, out receiverHero)) return;

            PartyBase receiverParty = null;
            if (obj.What.ReceiverIds.Item2 != null && !objectManager.TryGetObjectWithLogging(obj.What.ReceiverIds.Item2, out receiverParty)) return;

            notificationsBehavior.OnHeroOrPartyGaveItem((giverHero, giverParty), (receiverHero, receiverParty), obj.What.ItemRosterElement, obj.What.ShowNotification);
        });
    }

    private void Handle_NotifyRebellionFinished(MessagePayload<NotifyRebellionFinished> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Settlement, out var settlementId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.OldOwnerClan, out var oldOwnerClanId)) return;

            network.SendAll(new NetworkNotifyRebellionFinished(settlementId, oldOwnerClanId));
        });
    }

    private void Handle_NetworkNotifyRebellionFinished(MessagePayload<NetworkNotifyRebellionFinished> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.OldOwnerClanId, out var oldOwnerClan)) return;

            notificationsBehavior.OnRebellionFinished(settlement, oldOwnerClan);
        });
    }

    private void Handle_NotifyTournamentFinished(MessagePayload<NotifyTournamentFinished> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Winner, out var winnerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Town, out var townId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Prize, out var prizeId)) return;

            var participantsIds = new MBReadOnlyList<string>();
            foreach (var participant in obj.What.Participants)
            {
                if (!objectManager.TryGetIdWithLogging(participant, out var participantId)) continue;

                participantsIds.Add(participantId);
            }

            network.SendAll(new NetworkNotifyTournamentFinished(winnerId, participantsIds, townId, prizeId));
        });
    }

    private void Handle_NetworkNotifyTournamentFinished(MessagePayload<NetworkNotifyTournamentFinished> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(obj.What.WinnerId, out var winner)) return;
            if (!objectManager.TryGetObjectWithLogging<Town>(obj.What.TownId, out var town)) return;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(obj.What.PrizeId, out var prize)) return;

            var participants = new MBReadOnlyList<CharacterObject>();
            foreach (var participantId in obj.What.ParticipantsIds)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(participantId, out var participant)) continue;

                participants.Add(participant);
            }

            notificationsBehavior.OnTournamentFinished(winner, participants, town, prize);
        });
    }

    private void Handle_NotifyBuildingLevelChanged(MessagePayload<NotifyBuildingLevelChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Town, out var townId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Building, out var buildingId)) return;

            network.SendAll(new NetworkNotifyBuildingLevelChanged(townId, buildingId, obj.What.LevelChange));
        });
    }

    private void Handle_NetworkNotifyBuildingLevelChanged(MessagePayload<NetworkNotifyBuildingLevelChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Town>(obj.What.TownId, out var town)) return;
            if (!objectManager.TryGetObjectWithLogging<Building>(obj.What.BuildingId, out var building)) return;

            notificationsBehavior.OnBuildingLevelChanged(town, building, obj.What.LevelChange);
        });
    }

    private void Handle_NotifyHeroTeleportation(MessagePayload<NotifyHeroTeleportation> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

            string targetSettlementId = null;
            if (obj.What.TargetSettlement != null && !objectManager.TryGetIdWithLogging(obj.What.TargetSettlement, out targetSettlementId)) return;

            string targetPartyId = null;
            if (obj.What.TargetParty != null && !objectManager.TryGetIdWithLogging(obj.What.TargetParty, out targetPartyId)) return;

            network.SendAll(new NetworkNotifyHeroTeleportation(heroId, targetSettlementId, targetPartyId, obj.What.Detail));
        });
    }

    private void Handle_NetworkNotifyHeroTeleportation(MessagePayload<NetworkNotifyHeroTeleportation> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetNotificationsBehavior(out var notificationsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            Settlement targetSettlement = null;
            if (obj.What.TargetSettlementId != null && !objectManager.TryGetObjectWithLogging(obj.What.TargetSettlementId, out targetSettlement)) return;

            MobileParty targetParty = null;
            if (obj.What.TargetPartyId != null && !objectManager.TryGetObjectWithLogging(obj.What.TargetPartyId, out targetParty)) return;

            // Skip check for hero travelling as when client runs this check the hero state has already changed to active from travelling
            if (obj.What.Detail == TeleportHeroAction.TeleportationDetail.ImmediateTeleportToSettlement && hero.Clan == Clan.PlayerClan && targetSettlement.IsFortification && targetSettlement.Town.Governor == hero)
            {
                TextObject textObject3 = new TextObject("{=btynhBAn}The new governor of {SETTLEMENT}, {HERO.NAME}, has arrived and taken up the reins of office.", null);
                textObject3.SetCharacterProperties("HERO", hero.CharacterObject, false);
                textObject3.SetTextVariable("SETTLEMENT", targetSettlement.Name);
                MBInformationManager.AddQuickInformation(textObject3, 0, null, null, "");
                return;
            }

            notificationsBehavior.OnHeroTeleportationRequested(hero, targetSettlement, targetParty, obj.What.Detail);
        });
    }

    private bool TryGetNotificationsBehavior(out DefaultNotificationsCampaignBehavior campaignBehavior)
    {
        campaignBehavior = Campaign.Current?.GetCampaignBehavior<DefaultNotificationsCampaignBehavior>();
        if (campaignBehavior != null) return true;

        Logger.Debug("Skipping notification because DefaultNotificationsCampaignBehavior is unavailable");
        return false;
    }

    private bool TryGetViewDataTrackerBehavior(out ViewDataTrackerCampaignBehavior viewDataTrackerBehavior)
    {
        viewDataTrackerBehavior = Campaign.Current?.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>();
        if (viewDataTrackerBehavior != null) return true;

        Logger.Debug("Skipping view data tracker update because ViewDataTrackerCampaignBehavior is unavailable");
        return false;
    }
}