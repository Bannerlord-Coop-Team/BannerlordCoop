using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Regression coverage for co-op food-consumption eligibility and legacy starvation repair.
/// </summary>
[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class MobilePartyFoodConsumptionModelPatchesTests : IDisposable
{
    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
    private readonly List<object> registeredPlayerObjects = new();

    public void Dispose()
    {
        foreach (var playerObject in registeredPlayerObjects)
        {
            playerObjects.Remove(playerObject);
        }
    }

    [Fact]
    public void DoesPartyConsumeFood_ActiveLeaderlessParty_ReturnsTrue()
    {
        var party = CreateActiveParty();

        Assert.True(DoesPartyConsumeFood(party));
    }

    [Fact]
    public void DoesPartyConsumeFood_InactiveParty_ReturnsFalse()
    {
        var party = CreateActiveParty();
        party.IsActive = false;

        Assert.False(DoesPartyConsumeFood(party));
    }

    [Fact]
    public void DoesPartyConsumeFood_VanillaExcludedParties_ReturnFalse()
    {
        var excludedParties = new (string Name, Action<MobileParty> Configure)[]
        {
            (nameof(MobileParty.IsGarrison), party => party.IsGarrison = true),
            (nameof(MobileParty.IsCaravan), party => party.IsCaravan = true),
            (nameof(MobileParty.IsBandit), party => party.IsBandit = true),
            (nameof(MobileParty.IsMilitia), party => party.IsMilitia = true),
            (nameof(MobileParty.IsPatrolParty), party => party.IsPatrolParty = true),
            (nameof(MobileParty.IsVillager), party => party.IsVillager = true),
        };

        foreach (var excludedParty in excludedParties)
        {
            var party = CreateActiveParty();
            excludedParty.Configure(party);

            Assert.False(DoesPartyConsumeFood(party), excludedParty.Name);
        }
    }

    [Fact]
    public void DoesPartyConsumeFood_CoopPlayerClanParty_ReturnsTrue()
    {
        var party = CreatePlayerClanParty();

        Assert.True(DoesPartyConsumeFood(party));
    }

    [Fact]
    public void DoesPartyConsumeFood_CoopPlayerClanMilitia_ReturnsFalse()
    {
        var party = CreatePlayerClanParty();
        party.IsMilitia = true;

        Assert.False(DoesPartyConsumeFood(party));
    }

    [Fact]
    public void DoesPartyConsumeFood_UnqualifiedLeader_ReturnsFalse()
    {
        var leader = CreateLeader(Occupation.NotAssigned, isMinorFactionHero: false);
        var party = CreatePartyWithLeader(leader);

        Assert.False(DoesPartyConsumeFood(party));
    }

    [Fact]
    public void DoesPartyConsumeFood_LordAndMinorFactionLeaders_ReturnTrue()
    {
        var lordParty = CreatePartyWithLeader(CreateLeader(Occupation.Lord, isMinorFactionHero: false));
        var minorFactionParty = CreatePartyWithLeader(CreateLeader(Occupation.NotAssigned, isMinorFactionHero: true));

        Assert.True(DoesPartyConsumeFood(lordParty));
        Assert.True(DoesPartyConsumeFood(minorFactionParty));
    }

    [Fact]
    public void RepairNonConsumingPartyFoodState_NonConsumerClearsPersistedStarvation()
    {
        var party = CreatePartyWithFoodDebt(isActive: true);

        bool repaired = FoodConsumptionBehaviorInterface.RepairNonConsumingPartyFoodState(party, doesPartyConsumeFood: false);

        Assert.True(repaired);
        Assert.Equal(0, party.Party.RemainingFoodPercentage);
        Assert.False(party.Party.IsStarving);
    }

    [Fact]
    public void RepairNonConsumingPartyFoodState_ConsumerLeavesFoodDebtUntouched()
    {
        var party = CreatePartyWithFoodDebt(isActive: true);

        bool repaired = FoodConsumptionBehaviorInterface.RepairNonConsumingPartyFoodState(party, doesPartyConsumeFood: true);

        Assert.False(repaired);
        Assert.Equal(-100, party.Party.RemainingFoodPercentage);
        Assert.True(party.Party.IsStarving);
    }

    [Fact]
    public void RepairNonConsumingPartyFoodState_InactivePartyLeavesFoodDebtUntouched()
    {
        var party = CreatePartyWithFoodDebt(isActive: false);

        bool repaired = FoodConsumptionBehaviorInterface.RepairNonConsumingPartyFoodState(party, doesPartyConsumeFood: false);

        Assert.False(repaired);
        Assert.Equal(-100, party.Party.RemainingFoodPercentage);
    }

    [Fact]
    public void RepairNonConsumingPartyFoodState_PositiveFoodStateRemainsUntouched()
    {
        var party = CreatePartyWithFoodDebt(isActive: true);
        party.Party.RemainingFoodPercentage = 25;

        bool repaired = FoodConsumptionBehaviorInterface.RepairNonConsumingPartyFoodState(party, doesPartyConsumeFood: false);

        Assert.False(repaired);
        Assert.Equal(25, party.Party.RemainingFoodPercentage);
    }

    private static MobileParty CreateActiveParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        return party;
    }

    private MobileParty CreatePlayerClanParty()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var controllerIdProvider = new ControllerIdProvider();
        controllerIdProvider.SetControllerId("PlayerOne");
        playerObjects.Add(clan, new ControlledObjectInfo("PlayerOne", controllerIdProvider));
        registeredPlayerObjects.Add(clan);

        var leader = CreateLeader(Occupation.NotAssigned, isMinorFactionHero: false);
        leader._clan = clan;
        return CreatePartyWithLeader(leader);
    }

    private static Hero CreateLeader(Occupation occupation, bool isMinorFactionHero)
    {
        var leader = ObjectHelper.SkipConstructor<Hero>();
        leader.Occupation = occupation;
        leader.IsMinorFactionHero = isMinorFactionHero;
        return leader;
    }

    private static MobileParty CreatePartyWithLeader(Hero leader)
    {
        var component = ObjectHelper.SkipConstructor<CustomPartyComponent>();
        component._leader = leader;

        var party = CreateActiveParty();
        party._partyComponent = component;
        return party;
    }

    private static MobileParty CreatePartyWithFoodDebt(bool isActive)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = isActive;
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        party.Party.RemainingFoodPercentage = -100;
        return party;
    }

    private static bool DoesPartyConsumeFood(MobileParty party)
    {
        bool result = false;
        bool runOriginal = MobilePartyFoodConsumptionModelPatches.DoesPartyConsumeFoodPrefix(
            new DefaultMobilePartyFoodConsumptionModel(),
            ref result,
            party);

        Assert.False(runOriginal);
        return result;
    }
}
