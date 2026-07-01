using Common.Messaging;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyAllianceStarted : IEvent
{
    public readonly Kingdom Kingdom1;
    public readonly Kingdom Kingdom2;

    public NotifyAllianceStarted(
        Kingdom kingdom1,
        Kingdom kingdom2)
    {
        Kingdom1 = kingdom1;
        Kingdom2 = kingdom2;
    }
}

public readonly struct NotifyAllianceEnded : IEvent
{
    public readonly Kingdom Kingdom1;
    public readonly Kingdom Kingdom2;

    public NotifyAllianceEnded(
        Kingdom kingdom1,
        Kingdom kingdom2)
    {
        Kingdom1 = kingdom1;
        Kingdom2 = kingdom2;
    }
}

public readonly struct NotifyCallWarToWarAgreementStarted : IEvent
{
    public readonly Kingdom CallingKingdom;
    public readonly Kingdom CalledKingdom;
    public readonly Kingdom KingdomToCallToWarAgainst;

    public NotifyCallWarToWarAgreementStarted(
        Kingdom callingKingdom,
        Kingdom calledKingdom,
        Kingdom kingdomToCallToWarAgainst)
    {
        CallingKingdom = callingKingdom;
        CalledKingdom = calledKingdom;
        KingdomToCallToWarAgainst = kingdomToCallToWarAgainst;
    }
}

public readonly struct NotifyCallWarToWarAgreementEnded : IEvent
{
    public readonly Kingdom CallingKingdom;
    public readonly Kingdom CalledKingdom;
    public readonly Kingdom KingdomToCallToWarAgainst;

    public NotifyCallWarToWarAgreementEnded(
        Kingdom callingKingdom,
        Kingdom calledKingdom,
        Kingdom kingdomToCallToWarAgainst)
    {
        CallingKingdom = callingKingdom;
        CalledKingdom = calledKingdom;
        KingdomToCallToWarAgainst = kingdomToCallToWarAgainst;
    }
}

public readonly struct NotifySettlementEntered : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly Settlement Settlement;
    public readonly Hero Hero;

    public NotifySettlementEntered(
        MobileParty mobileParty,
        Settlement settlement,
        Hero hero)
    {
        MobileParty = mobileParty;
        Settlement = settlement;
        Hero = hero;
    }
}

public readonly struct NotifyPartyAddedToMapEvent : IEvent
{
    public readonly PartyBase InvolvedParty;

    public NotifyPartyAddedToMapEvent(
        PartyBase involvedParty)
    {
        InvolvedParty = involvedParty;
    }
}

public readonly struct NotifyCompanionRemoved : IEvent
{
    public readonly Clan Clan;
    public readonly Hero Hero;
    public readonly RemoveCompanionAction.RemoveCompanionDetail Detail;

    public NotifyCompanionRemoved(
        Clan clan,
        Hero hero,
        RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        Clan = clan;
        Hero = hero;
        Detail = detail;
    }
}

public readonly struct NotifyRenownGained : IEvent
{
    public readonly Hero Hero;
    public readonly int GainedRenown;
    public readonly bool DoNotNotifyPlayer;

    public NotifyRenownGained(
        Hero hero,
        int gainedRenown,
        bool doNotNotifyPlayer)
    {
        Hero = hero;
        GainedRenown = gainedRenown;
        DoNotNotifyPlayer = doNotNotifyPlayer;
    }
}

public readonly struct NotifyHideoutSpotted : IEvent
{
    public readonly PartyBase SpottingParty;
    public readonly PartyBase HideoutParty;

    public NotifyHideoutSpotted(
        PartyBase spottingParty,
        PartyBase hideoutParty)
    {
        SpottingParty = spottingParty;
        HideoutParty = hideoutParty;
    }
}

public readonly struct NotifyHeroBecameFugitive : IEvent
{
    public readonly Hero Hero;
    public readonly bool ShowNotification;

    public NotifyHeroBecameFugitive(
        Hero hero,
        bool showNotification)
    {
        Hero = hero;
        ShowNotification = showNotification;
    }
}

public readonly struct NotifyPrisonerTaken : IEvent
{
    public readonly PartyBase Capturer;
    public readonly Hero Prisoner;

    public NotifyPrisonerTaken(
        PartyBase capturer,
        Hero prisoner)
    {
        Capturer = capturer;
        Prisoner = prisoner;
    }
}

public readonly struct NotifyHeroPrisonerReleased : IEvent
{
    public readonly Hero Hero;
    public readonly PartyBase Party;
    public readonly IFaction CapturerFaction;
    public readonly EndCaptivityDetail Detail;
    public readonly bool ShowNotification;

    public NotifyHeroPrisonerReleased(
        Hero hero,
        PartyBase party,
        IFaction capturerFaction,
        EndCaptivityDetail detail,
        bool showNotification)
    {
        Hero = hero;
        Party = party;
        CapturerFaction = capturerFaction;
        Detail = detail;
        ShowNotification = showNotification;
    }
}

public readonly struct NotifyBattleStarted : IEvent
{
    public readonly PartyBase AttackerParty;
    public readonly PartyBase DefenderParty;
    public readonly Settlement Subject;
    public readonly bool ShowNotification;

    public NotifyBattleStarted(
        PartyBase attackerParty,
        PartyBase defenderParty,
        Settlement subject,
        bool showNotification)
    {
        AttackerParty = attackerParty;
        DefenderParty = defenderParty;
        Subject = subject;
        ShowNotification = showNotification;
    }
}

public readonly struct NotifySiegeEventStarted : IEvent
{
    public readonly SiegeEvent SiegeEvent;

    public NotifySiegeEventStarted(
        SiegeEvent siegeEvent)
    {
        SiegeEvent = siegeEvent;
    }
}

public readonly struct NotifyClanTierIncreased : IEvent
{
    public readonly Clan Clan;
    public readonly bool ShouldNotify;

    public NotifyClanTierIncreased(
        Clan clan,
        bool shouldNotify)
    {
        Clan = clan;
        ShouldNotify = shouldNotify;
    }
}

public readonly struct NotifyItemsLooted : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly ItemRosterElement[] ItemRosterData;

    public NotifyItemsLooted(
        MobileParty mobileParty,
        ItemRosterElement[] itemRosterData)
    {
        MobileParty = mobileParty;
        ItemRosterData = itemRosterData;
    }
}

public readonly struct NotifyRelationChanged : IEvent
{
    public readonly Hero EffectiveHero;
    public readonly Hero EffectiveHeroGainedRelationWith;
    public readonly int RelationChange;
    public readonly bool ShowNotification;
    public readonly ChangeRelationAction.ChangeRelationDetail Detail;
    public readonly Hero OriginalHero;
    public readonly Hero OriginalGainedRelationWith;

    public NotifyRelationChanged(
        Hero effectiveHero,
        Hero effectiveHeroGainedRelationWith,
        int relationChange,
        bool showNotification,
        ChangeRelationAction.ChangeRelationDetail detail,
        Hero originalHero,
        Hero originalGainedRelationWith)
    {
        EffectiveHero = effectiveHero;
        EffectiveHeroGainedRelationWith = effectiveHeroGainedRelationWith;
        RelationChange = relationChange;
        ShowNotification = showNotification;
        Detail = detail;
        OriginalHero = originalHero;
        OriginalGainedRelationWith = originalGainedRelationWith;
    }
}

public readonly struct NotifyHeroLevelledUp : IEvent
{
    public readonly Hero Hero;
    public readonly bool ShouldNotify;

    public NotifyHeroLevelledUp(
        Hero hero,
        bool shouldNotify)
    {
        Hero = hero;
        ShouldNotify = shouldNotify;
    }
}

public readonly struct NotifyHeroGainedSkill : IEvent
{
    public readonly Hero Hero;
    public readonly SkillObject Skill;
    public readonly int Change;
    public readonly bool ShouldNotify;

    public NotifyHeroGainedSkill(
        Hero hero,
        SkillObject skill,
        int change,
        bool shouldNotify)
    {
        Hero = hero;
        Skill = skill;
        Change = change;
        ShouldNotify = shouldNotify;
    }
}

public readonly struct NotifyTroopsDeserted : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly TroopRoster DesertedTroops;

    public NotifyTroopsDeserted(
        MobileParty mobileParty,
        TroopRoster desertedTroops)
    {
        MobileParty = mobileParty;
        DesertedTroops = desertedTroops;
    }
}

public readonly struct NotifyClanChangedFaction : IEvent
{
    public readonly Clan Clan;
    public readonly Kingdom OldKingdom;
    public readonly Kingdom NewKingdom;
    public readonly ChangeKingdomAction.ChangeKingdomActionDetail Detail;
    public readonly bool ShowNotification;

    public NotifyClanChangedFaction(
        Clan clan,
        Kingdom oldKingdom,
        Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail detail,
        bool showNotification)
    {
        Clan = clan;
        OldKingdom = oldKingdom;
        NewKingdom = newKingdom;
        Detail = detail;
        ShowNotification = showNotification;
    }
}

public readonly struct NotifyArmyCreated : IEvent
{
    public readonly Army Army;

    public NotifyArmyCreated(Army army)
    {
        Army = army;
    }
}

public readonly struct NotifySiegeBombardmentHit : IEvent
{
    public readonly MobileParty BesiegerParty;
    public readonly Settlement BesiegedSettlement;
    public readonly BattleSideEnum Side;
    public readonly SiegeEngineType Weapon;
    public readonly SiegeBombardTargets Target;

    public NotifySiegeBombardmentHit(
        MobileParty besiegerParty,
        Settlement besiegedSettlement,
        BattleSideEnum side,
        SiegeEngineType weapon,
        SiegeBombardTargets target)
    {
        BesiegerParty = besiegerParty;
        BesiegedSettlement = besiegedSettlement;
        Side = side;
        Weapon = weapon;
        Target = target;
    }
}

public readonly struct NotifySiegeBombardmentWallHit : IEvent
{
    public readonly MobileParty BesiegerParty;
    public readonly Settlement BesiegedSettlement;
    public readonly BattleSideEnum Side;
    public readonly SiegeEngineType Weapon;
    public readonly bool IsWallCracked;

    public NotifySiegeBombardmentWallHit(
        MobileParty besiegerParty,
        Settlement besiegedSettlement,
        BattleSideEnum side,
        SiegeEngineType weapon,
        bool isWallCracked)
    {
        BesiegerParty = besiegerParty;
        BesiegedSettlement = besiegedSettlement;
        Side = side;
        Weapon = weapon;
        IsWallCracked = isWallCracked;
    }
}

public readonly struct NotifySiegeEngineDestroyed : IEvent
{
    public readonly MobileParty BesiegerParty;
    public readonly Settlement BesiegedSettlement;
    public readonly BattleSideEnum Side;
    public readonly SiegeEngineType DestroyedEngine;

    public NotifySiegeEngineDestroyed(
        MobileParty besiegerParty,
        Settlement besiegedSettlement,
        BattleSideEnum side,
        SiegeEngineType destroyedEngine)
    {
        BesiegerParty = besiegerParty;
        BesiegedSettlement = besiegedSettlement;
        Side = side;
        DestroyedEngine = destroyedEngine;
    }
}

public readonly struct NotifyPartyJoinedArmy : IEvent
{
    public readonly MobileParty MobileParty;

    public NotifyPartyJoinedArmy(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}

public readonly struct NotifyPartyAttachedAnotherParty : IEvent
{
    public readonly MobileParty MobileParty;

    public NotifyPartyAttachedAnotherParty(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}

public readonly struct NotifyPartyRemovedFromArmy : IEvent
{
    public readonly MobileParty MobileParty;

    public NotifyPartyRemovedFromArmy(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}

public readonly struct ArmyDispersed : IEvent {}

public readonly struct NotifyHeroesMarried : IEvent
{
    public readonly Hero FirstHero;
    public readonly Hero SecondHero;
    public readonly bool ShowNotification;

    public NotifyHeroesMarried(
        Hero firstHero,
        Hero secondHero,
        bool showNotification)
    {
        FirstHero = firstHero;
        SecondHero = secondHero;
        ShowNotification = showNotification;
    }
}

public readonly struct NotifyChildConceived : IEvent
{
    public readonly Hero Mother;

    public NotifyChildConceived(Hero mother)
    {
        Mother = mother;
    }
}

public readonly struct NotifyGivenBirth : IEvent
{
    public readonly Hero Mother;
    public readonly List<Hero> AliveOffsprings;
    public readonly int StillbornCount;

    public NotifyGivenBirth(
        Hero mother,
        List<Hero> aliveOffsprings,
        int stillbornCount)
    {
        Mother = mother;
        AliveOffsprings = aliveOffsprings;
        StillbornCount = stillbornCount;
    }
}

public readonly struct NotifyHeroKilled : IEvent
{
    public readonly Hero VictimHero;
    public readonly Hero Killer;
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;
    public readonly bool ShowNotification;

    public NotifyHeroKilled(
        Hero victimHero,
        Hero killer,
        KillCharacterAction.KillCharacterActionDetail detail,
        bool showNotification)
    {
        VictimHero = victimHero;
        Killer = killer;
        Detail = detail;
        ShowNotification = showNotification;
    }
}

public readonly struct HeroSharedFoodWithAnotherHero : IEvent
{
    public readonly Hero SupporterHero;
    public readonly Hero SupportedHero;
    public readonly float Influence;

    public HeroSharedFoodWithAnotherHero(
        Hero supporterHero,
        Hero supportedHero,
        float influence)
    {
        SupporterHero = supporterHero;
        SupportedHero = supportedHero;
        Influence = influence;
    }
}

public readonly struct NotifyClanDestroyed : IEvent
{
    public readonly TextObject DestroyedClanName;

    public NotifyClanDestroyed(
        TextObject destroyedClanName)
    {
        DestroyedClanName = destroyedClanName;
    }
}

public readonly struct NotifyHeroOrPartyGaveItem : IEvent
{
    public readonly ValueTuple<Hero, PartyBase> Giver;
    public readonly ValueTuple<Hero, PartyBase> Receiver;
    public readonly ItemRosterElement ItemRosterElement;
    public readonly bool ShowNotification;

    public NotifyHeroOrPartyGaveItem(
        (Hero, PartyBase) giver,
        (Hero, PartyBase) receiver,
        ItemRosterElement itemRosterElement,
        bool showNotification)
    {
        Giver = giver;
        Receiver = receiver;
        ItemRosterElement = itemRosterElement;
        ShowNotification = showNotification;
    }
}

public readonly struct NotifyRebellionFinished : IEvent
{
    public readonly Settlement Settlement;
    public readonly Clan OldOwnerClan;

    public NotifyRebellionFinished(
        Settlement settlement,
        Clan oldOwnerClan)
    {
        Settlement = settlement;
        OldOwnerClan = oldOwnerClan;
    }
}

public readonly struct NotifyTournamentFinished : IEvent
{
    public readonly CharacterObject Winner;
    public readonly MBReadOnlyList<CharacterObject> Participants;
    public readonly Town Town;
    public readonly ItemObject Prize;

    public NotifyTournamentFinished(
        CharacterObject winner,
        MBReadOnlyList<CharacterObject> participants,
        Town town,
        ItemObject prize)
    {
        Winner = winner;
        Participants = participants;
        Town = town;
        Prize = prize;
    }
}

public readonly struct NotifyBuildingLevelChanged : IEvent
{
    public readonly Town Town;
    public readonly Building Building;
    public readonly int LevelChange;

    public NotifyBuildingLevelChanged(
        Town town,
        Building building,
        int levelChange)
    {
        Town = town;
        Building = building;
        LevelChange = levelChange;
    }
}

public readonly struct NotifyHeroTeleportation : IEvent
{
    public readonly Hero Hero;
    public readonly Settlement TargetSettlement;
    public readonly MobileParty TargetParty;
    public readonly TeleportHeroAction.TeleportationDetail Detail;

    public NotifyHeroTeleportation(
        Hero hero,
        Settlement targetSettlement,
        MobileParty targetParty,
        TeleportHeroAction.TeleportationDetail detail)
    {
        Hero = hero;
        TargetSettlement = targetSettlement;
        TargetParty = targetParty;
        Detail = detail;
    }
}