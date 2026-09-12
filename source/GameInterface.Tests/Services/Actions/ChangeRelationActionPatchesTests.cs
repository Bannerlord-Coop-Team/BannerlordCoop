using Common;
using GameInterface.Services.Actions.Patches;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Actions;

[Collection(ModInformationRoleCollection.Name)]
public class ChangeRelationActionPatchesTests
{
    public ChangeRelationActionPatchesTests()
    {
        GameBootStrap.Initialize();
    }

    [Fact]
    public void ClientAlwaysSkipsNativeApplyInternal()
    {
        Hero source = Raw<Hero>();
        Hero target = Raw<Hero>();

        Assert.False(RunPrefix(source, target, relationChange: 1, isServer: false));
    }

    [Fact]
    public void ServerRunsNativeApplyInternalForValidEffectiveHeroes()
    {
        Hero source = HeroWithSelfLedClan();
        Hero target = HeroWithSelfLedClan();

        Assert.True(RunPrefix(source, target, relationChange: 1, isServer: true));
    }

    [Fact]
    public void ServerSkipsRelationChangeWithNullOriginalHero()
    {
        Hero target = Raw<Hero>();

        Assert.False(RunPrefix(null, target, relationChange: 1, isServer: true));
    }

    [Fact]
    public void ServerSkipsRelationChangeWhenClanLeaderMakesEffectiveHeroNull()
    {
        Hero source = Raw<Hero>();
        source._clan = Raw<Clan>();
        source._companionOf = source._clan;
        source._clan._leader = null;
        Hero target = HeroWithSelfLedClan();

        Assert.False(RunPrefix(source, target, relationChange: 1, isServer: true));
    }

    [Fact]
    public void ServerSkipsRelationChangeWhenTargetClanLeaderMakesEffectiveHeroNull()
    {
        Hero source = HeroWithSelfLedClan();
        Hero target = Raw<Hero>();
        target._clan = Raw<Clan>();
        target._companionOf = target._clan;
        target._clan._leader = null;

        Assert.False(RunPrefix(source, target, relationChange: 1, isServer: true));
    }

    [Fact]
    public void ServerPreservesDiplomacyModelFallbackWhenBothClanLeadersAreNull()
    {
        Hero source = Raw<Hero>();
        source._clan = Raw<Clan>();
        source._companionOf = source._clan;
        source._clan._leader = null;
        Hero target = Raw<Hero>();
        target._clan = Raw<Clan>();
        target._companionOf = target._clan;
        target._clan._leader = null;

        Assert.True(RunPrefix(source, target, relationChange: 1, isServer: true));
    }

    [Fact]
    public void ServerPreservesNativeNoOpWithoutResolvingHeroes()
    {
        Assert.True(RunPrefix(null, null, relationChange: 0, isServer: true));
    }

    [Fact]
    public void ApplyInternalTargetHasExpectedSignature()
    {
        MethodInfo target = AccessTools.Method(
            typeof(ChangeRelationAction),
            nameof(ChangeRelationAction.ApplyInternal),
            new[]
            {
                typeof(Hero),
                typeof(Hero),
                typeof(int),
                typeof(bool),
                typeof(ChangeRelationAction.ChangeRelationDetail),
            });

        Assert.NotNull(target);
    }

    private static bool RunPrefix(Hero? source, Hero? target, int relationChange, bool isServer)
    {
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;
            return ChangeRelationActionPatches.ApplyInternalPrefix(source, target, relationChange);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

    private static T Raw<T>() where T : class =>
        (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static Hero HeroWithSelfLedClan()
    {
        Hero hero = Raw<Hero>();
        hero._clan = Raw<Clan>();
        hero._companionOf = hero._clan;
        hero._clan._leader = hero;
        return hero;
    }
}
