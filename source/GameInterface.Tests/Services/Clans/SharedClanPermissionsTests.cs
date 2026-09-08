using Autofac;
using Common.Util;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.Players;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

[Collection(nameof(CampaignCurrentCollection))]
public class SharedClanPermissionsTests : IDisposable
{
    private readonly SharedClanPermissions permissions = new(Mock.Of<IClanMemberGrouping>());
    private readonly Clan clan = ObjectHelper.SkipConstructor<Clan>();
    private readonly Hero leader;
    private readonly Hero member;
    private readonly Hero companion;
    private readonly MobileParty leaderParty = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly MobileParty memberParty = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly Game previousGame = Game.Current;
    private readonly ILifetimeScope previousContainer;
    private readonly IContainer container;
    private readonly List<object> registeredObjects = new();
    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools.Field(typeof(PlayerManager), "PlayerObjects").GetValue(null);

    public SharedClanPermissionsTests()
    {
        leader = CreateHero(leaderParty);
        member = CreateHero(memberParty);
        companion = CreateHero();
        clan._leader = leader;
        RegisterPlayerObject(leader);
        RegisterPlayerObject(member);
        RegisterPlayerObject(leaderParty);
        RegisterPlayerObject(memberParty);

        ContainerProvider.TryGetContainer(out previousContainer);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(permissions).As<ISharedClanPermissions>();
        container = builder.Build();
        ContainerProvider.SetContainer(container);
        Game.Current = ObjectHelper.SkipConstructor<Game>();
        SetViewer(member);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayersCanManageHeroesInTheirOwnParty(bool asLeader)
    {
        var actor = asLeader ? leader : member;
        companion._partyBelongedTo = actor.PartyBelongedTo;

        Assert.True(permissions.CanManageHero(actor, companion));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OtherPlayersPartyPreventsManagementAndRecallEvenForLeader(bool asLeader)
    {
        var actor = asLeader ? leader : member;
        companion._partyBelongedTo = asLeader ? memberParty : leaderParty;

        Assert.False(permissions.CanManageHero(actor, companion));
        Assert.False(permissions.CanRecallHero(actor, companion));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void MembersCanRecallOnlyHeroesWithoutPartyOrGovernorship(int assignment, bool expected)
    {
        if (assignment == 1) companion._partyBelongedTo = memberParty;
        if (assignment == 2) companion._partyBelongedTo = ObjectHelper.SkipConstructor<MobileParty>();
        if (assignment == 3) companion.GovernorOf = ObjectHelper.SkipConstructor<Town>();

        Assert.Equal(expected, permissions.CanRecallHero(member, companion));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeaderCanManageAndRecallHeroesWithNpcAssignments(bool governor)
    {
        if (governor) companion.GovernorOf = ObjectHelper.SkipConstructor<Town>();
        else companion._partyBelongedTo = ObjectHelper.SkipConstructor<MobileParty>();

        Assert.True(permissions.CanManageHero(leader, companion));
        Assert.True(permissions.CanRecallHero(leader, companion));
        Assert.False(permissions.CanManageHero(member, companion));
        Assert.False(permissions.CanRecallHero(member, companion));
    }

    [Fact]
    public void IdleHeroesCanBeRecalledByMembersButOnlyManagedByLeader()
    {
        Assert.False(permissions.CanManageHero(member, companion));
        Assert.True(permissions.CanRecallHero(member, companion));
        Assert.True(permissions.CanManageHero(leader, companion));
        Assert.True(permissions.CanRecallHero(leader, companion));
    }

    [Fact]
    public void PlayerHeroesRemainProtectedEvenWithoutParty()
    {
        member._partyBelongedTo = null;

        Assert.False(permissions.CanManageHero(leader, member));
        Assert.False(permissions.CanRecallHero(leader, member));
        Assert.True(permissions.CanManageHero(member, member));
        Assert.False(permissions.CanRecallHero(member, member));
    }

    [Fact]
    public void OtherClansHeroesCannotBeManagedOrRecalled()
    {
        companion._clan = ObjectHelper.SkipConstructor<Clan>();
        companion._partyBelongedTo = memberParty;

        Assert.False(permissions.CanManageHero(member, companion));
        Assert.False(permissions.CanRecallHero(leader, companion));
    }

    [Fact]
    public void MissingHeroOrActorCannotBeManagedOrRecalled()
    {
        Assert.False(permissions.CanManageHero(null, companion));
        Assert.False(permissions.CanManageHero(member, null));
        Assert.False(permissions.CanRecallHero(null, companion));
        Assert.False(permissions.CanRecallHero(member, null));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ClanManagementOptionsRequireLeaderAndKeepVanillaRestrictions(bool asLeader, bool initiallyAvailable)
    {
        SetViewer(asLeader ? leader : member);
        foreach (var patch in new ConditionPatch[]
        {
            SharedClanDialoguePatches.CompanionFireConditionPostfix,
            SharedClanDialoguePatches.LeadAPartyClickableConditionPostfix,
            SharedClanDialoguePatches.ConversationHeroHireOnConditionPostfix,
            SharedClanDialoguePatches.ConversationCaravanBuildOnConditionPostfix,
            SharedClanDialoguePatches.CanPlayerBuyWorkshopClickableConditionPostfix,
        })
        {
            bool result = initiallyAvailable;
            patch(ref result);
            Assert.Equal(asLeader && initiallyAvailable, result);
        }
    }

    [Fact]
    public void RecallConfirmationRejectsHeroWhoJoinedAnotherPartyAfterInquiryOpened()
    {
        SetViewer(leader);
        var item = ObjectHelper.SkipConstructor<ClanLordItemVM>();
        AccessTools.Field(typeof(ClanLordItemVM), "_hero").SetValue(item, companion);
        var vm = ObjectHelper.SkipConstructor<ClanMembersVM>();
        vm._currentSelectedMember = item;

        Assert.True(ClanMembersVMPatches.OnRequestRecallPrefix(vm));
        companion._partyBelongedTo = memberParty;
        Assert.False(ClanMembersVMPatches.OnConfirmRecallPrefix(vm));
    }

    private Hero CreateHero(MobileParty party = null)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._clan = clan;
        hero._partyBelongedTo = party;
        hero._characterObject = ObjectHelper.SkipConstructor<CharacterObject>();
        hero._characterObject._heroObject = hero;
        return hero;
    }

    private static void SetViewer(Hero hero) => Game.Current.PlayerTroop = hero.CharacterObject;

    private void RegisterPlayerObject(object obj)
    {
        playerObjects.Add(obj, new ControlledObjectInfo("shared-clan-test", null));
        registeredObjects.Add(obj);
    }

    public void Dispose()
    {
        foreach (var obj in registeredObjects) playerObjects.Remove(obj);
        Game.Current = previousGame;
        if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
        else ContainerProvider.Clear();
        container.Dispose();
    }

    private delegate void ConditionPatch(ref bool result);
}
