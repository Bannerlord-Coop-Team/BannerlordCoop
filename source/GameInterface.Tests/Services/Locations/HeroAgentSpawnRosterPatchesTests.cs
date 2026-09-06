using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.Locations.Patches;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

[Collection(ModInformationRoleCollection.Name)]
public class HeroAgentSpawnRosterPatchesTests
{
    public HeroAgentSpawnRosterPatchesTests()
    {
        GameBootStrap.Initialize();
    }

    [Fact]
    public void SettlementEntered_NotifiesExplicitHero()
    {
        Settlement settlement = CreateFortification();
        Hero hero = CreateHero("entered-hero");

        List<SettlementRosterHeroesChanged> published = CaptureNotifications(() =>
            InvokePatch("OnSettlementEnteredPostfix", null, settlement, hero));

        SettlementRosterHeroesChanged notification = Assert.Single(published);
        Assert.Same(settlement, notification.Settlement);
        Assert.Equal(new[] { hero }, notification.Heroes);
    }

    [Fact]
    public void SettlementEntered_UsesAiPartyLeaderWhenHeroIsNull()
    {
        Settlement settlement = CreateFortification();
        Hero leader = CreateHero("party-leader");
        MobileParty party = CreateParty(leader);

        List<SettlementRosterHeroesChanged> published = CaptureNotifications(() =>
            InvokePatch("OnSettlementEnteredPostfix", party, settlement, null));

        SettlementRosterHeroesChanged notification = Assert.Single(published);
        Assert.Equal(new[] { leader }, notification.Heroes);
    }

    [Fact]
    public void GovernorChanged_NotifiesRemovedAndAddedGovernors()
    {
        Settlement settlement = CreateFortification();
        Hero oldGovernor = CreateHero("old-governor");
        Hero newGovernor = CreateHero("new-governor");

        List<SettlementRosterHeroesChanged> published = CaptureNotifications(() =>
            InvokePatch("OnGovernorChangedPostfix", settlement.Town, oldGovernor, newGovernor));

        SettlementRosterHeroesChanged notification = Assert.Single(published);
        Assert.Same(settlement, notification.Settlement);
        Assert.Equal(new[] { oldGovernor, newGovernor }, notification.Heroes);
    }

    [Fact]
    public void PrisonersChanged_NotifiesDirectAndRosterHeroes()
    {
        Settlement settlement = CreateFortification();
        Hero directPrisoner = CreateHero("direct-prisoner");
        Hero rosterPrisoner = CreateHero("roster-prisoner");
        var roster = new FlattenedTroopRoster(1);
        var descriptor = new UniqueTroopDescriptor(1);
        roster[descriptor] = new FlattenedTroopRosterElement(
            rosterPrisoner.CharacterObject,
            0,
            0,
            descriptor,
            0);

        List<SettlementRosterHeroesChanged> published = CaptureNotifications(() =>
            InvokePatch(
                "OnPrisonersChangeInSettlementPostfix",
                settlement,
                roster,
                directPrisoner));

        SettlementRosterHeroesChanged notification = Assert.Single(published);
        Assert.Equal(new[] { directPrisoner, rosterPrisoner }, notification.Heroes);
    }

    [Fact]
    public void EntryAndPrisonerNotifications_SkipNullOrIneligibleInputs()
    {
        Settlement nonFortification = new(new TextObject("Village"), null, null);

        List<SettlementRosterHeroesChanged> published = CaptureNotifications(() =>
        {
            InvokePatch("OnSettlementEnteredPostfix", null, null, null);
            InvokePatch("OnPrisonersChangeInSettlementPostfix", nonFortification, null, null);
        });

        Assert.Empty(published);
    }

    private static List<SettlementRosterHeroesChanged> CaptureNotifications(Action act)
    {
        var published = new List<SettlementRosterHeroesChanged>();
        Action<MessagePayload<SettlementRosterHeroesChanged>> capture = payload => published.Add(payload.What);
        bool wasServer = ModInformation.IsServer;
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            ModInformation.IsServer = true;
            act();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            MessageBroker.Instance.Unsubscribe(capture);
        }
        return published;
    }

    private static void InvokePatch(string methodName, params object[] arguments)
    {
        MethodInfo method = AccessTools.Method(typeof(HeroAgentSpawnRosterPatches), methodName);
        method.Invoke(null, arguments);
    }

    private static Hero CreateHero(string id)
    {
        var hero = new Hero { StringId = id };
        var character = new CharacterObject { StringId = id + "-character" };
        character.HeroObject = hero;
        hero._characterObject = character;
        return hero;
    }

    private static MobileParty CreateParty(Hero leader)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var component = ObjectHelper.SkipConstructor<CustomPartyComponent>();
        component._leader = leader;
        party._partyComponent = component;
        return party;
    }

    private static Settlement CreateFortification()
    {
        var settlement = new Settlement(new TextObject("Test Town"), null, null);
        var town = new Town { Owner = settlement.Party };
        settlement.SettlementComponent = town;
        settlement.Town = town;
        return settlement;
    }
}
