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
using TaleWorlds.CampaignSystem.Siege;
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
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom1Id, out var kingdom1)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom2Id, out var kingdom2)) return;

            CampaignEventDispatcher.Instance.OnAllianceStarted(kingdom1, kingdom2);
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
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom1Id, out var kingdom1)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom2Id, out var kingdom2)) return;

            CampaignEventDispatcher.Instance.OnAllianceEnded(kingdom1, kingdom2);
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
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CallingKingdomId, out var callingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CalledKingdomId, out var calledKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.KingdomToCallToWarAgainstId, out var kingdomToCallToWarAgainst)) return;

            CampaignEventDispatcher.Instance.OnCallToWarAgreementStarted(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
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
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CallingKingdomId, out var callingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.CalledKingdomId, out var calledKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.KingdomToCallToWarAgainstId, out var kingdomToCallToWarAgainst)) return;

            CampaignEventDispatcher.Instance.OnCallToWarAgreementEnded(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
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
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            CampaignEventDispatcher.Instance.OnSettlementEntered(mobileParty, settlement, hero);
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
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.InvolvedPartyId, out var involvedParty)) return;

            CampaignEventDispatcher.Instance.OnPartyAddedToMapEvent(involvedParty);
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
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            if (clan != Clan.PlayerClan) return;

            CampaignEventDispatcher.Instance.OnCompanionRemoved(hero, obj.What.Detail);
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
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            CampaignEventDispatcher.Instance.OnRenownGained(hero, obj.What.GainedRenown, obj.What.DoNotNotifyPlayer);
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
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.SpottingPartyId, out var spottingParty)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.HideoutPartyId, out var hideoutParty)) return;

            CampaignEventDispatcher.Instance.OnHideoutSpotted(spottingParty, hideoutParty);
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
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            CampaignEventDispatcher.Instance.OnCharacterBecameFugitive(hero, obj.What.ShowNotification);
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
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.CapturerId, out var capturer)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.PrisonerId, out var prisoner)) return;

            CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(capturer, prisoner);
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

            CampaignEventDispatcher.Instance.OnHeroPrisonerReleased(hero, party, capturerFaction, obj.What.Detail, obj.What.ShowNotification);
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
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.DefenderPartyId, out var defenderParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var subject)) return;

            CampaignEventDispatcher.Instance.OnStartBattle(attackerParty, defenderParty, subject, obj.What.ShowNotification);
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
            if (!objectManager.TryGetObjectWithLogging<SiegeEvent>(obj.What.SiegeEventId, out var siegeEvent)) return;

            CampaignEventDispatcher.Instance.OnSiegeEventStarted(siegeEvent);
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
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;

            CampaignEventDispatcher.Instance.OnClanTierChanged(clan, obj.What.ShouldNotify);
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
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            using (new AllowedThread())
            {
                ItemRoster lootedItems = new();
                lootedItems.Add(obj.What.ItemRosterData);

                CampaignEventDispatcher.Instance.OnItemsLooted(mobileParty, lootedItems);
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
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.EffectiveHeroId, out var effectiveHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.EffectiveHeroGainedRelationWithId, out var effectiveHeroGainedRelationWith)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OriginalHeroId, out var originalHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OriginalGainedRelationWithId, out var originalGainedRelationWith)) return;

            CampaignEventDispatcher.Instance.OnHeroRelationChanged(effectiveHero, effectiveHeroGainedRelationWith, obj.What.RelationChange, obj.What.ShowNotification, obj.What.Detail, originalHero, originalGainedRelationWith);
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
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            CampaignEventDispatcher.Instance.OnHeroLevelledUp(hero, obj.What.ShouldNotify);
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
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            using (new AllowedThread())
            {
                TroopRoster desertedTroops = new();
                foreach (var troop in troopRosterInterface.UnpackTroopRosterData(obj.What.DesertedTroopsData))
                {
                    desertedTroops.Add(troop);
                }

                CampaignEventDispatcher.Instance.OnTroopsDeserted(mobileParty, desertedTroops);
            }
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
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.DestroyedClanId, out var destroyedClan)) return;

            CampaignEventDispatcher.Instance.OnClanDestroyed(destroyedClan);
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
            if (!objectManager.TryGetObjectWithLogging<Town>(obj.What.TownId, out var town)) return;
            if (!objectManager.TryGetObjectWithLogging<Building>(obj.What.BuildingId, out var building)) return;

            CampaignEventDispatcher.Instance.OnBuildingLevelChanged(town, building, obj.What.LevelChange);
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

            CampaignEventDispatcher.Instance.OnHeroTeleportationRequested(hero, targetSettlement, targetParty, obj.What.Detail);
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