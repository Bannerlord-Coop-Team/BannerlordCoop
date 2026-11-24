using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.Players;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using NearbyPlayerSearch = GameInterface.Services.MobilePartyAIs.Patches.DefaultMobilePartyAIModelPatches.NearbyPlayerSearch;

namespace GameInterface.Tests.Services.MobilePartyAIs;

/// <summary>Exercises the nearby-player scoring cache against the game locator without loading a campaign map.</summary>
[Collection(nameof(CampaignCurrentCollection))]
public class DefaultMobilePartyAIModelPatchesTests : IDisposable
{
    private const float UnsuppressedScore = 4f;

    private static readonly ConditionalWeakTable<object, ControlledObjectInfo> PlayerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools.Field(typeof(PlayerManager), "PlayerObjects").GetValue(null)!;

    private readonly Campaign previousCampaign = Campaign.Current;
    private readonly List<MobileParty> registeredPlayers = new();

    public DefaultMobilePartyAIModelPatchesTests()
    {
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign._gameModels = ObjectHelper.SkipConstructor<GameModels>();
        campaign.Models.EncounterModel = new DefaultEncounterModel();
        Campaign.Current = campaign;
    }

    public void Dispose()
    {
        Campaign.Current = previousCampaign;
        foreach (var player in registeredPlayers)
            PlayerObjects.Remove(player);
    }

    [Fact]
    public void ParkedPlayerDoesNotSuppressUntilActive()
    {
        var attacker = CreateParty(100f);
        var enemy = CreateParty(50f);
        var parkedPlayer = AddPlayer(300f, x: 101f, active: false);
        AddPlayer(300f, x: 109f); // Default encounter radius is 3, and the locator excludes the boundary.
        Campaign.Current.MobilePartyLocator.UpdateLocator(CreateParty(300f, x: 102f));

        NearbyPlayerSearch nearbyPlayers = default;
        Assert.Equal(UnsuppressedScore, Score(attacker, enemy, ref nearbyPlayers));

        parkedPlayer.IsActive = true;
        nearbyPlayers = default;
        Assert.Equal(0f, Score(attacker, enemy, ref nearbyPlayers));
    }

    [Fact]
    public void SharedSearchResumesWhenThePreviouslySeenPlayerCannotSuppressAnotherEnemy()
    {
        var attacker = CreateParty(100f);
        var firstEnemy = CreateParty(50f);
        var secondEnemy = CreateParty(60f);
        AddPlayer(300f, x: 101f);
        AddPlayer(300f, x: 102f);
        var order = MobileParty.StartFindingLocatablesAroundPosition(attacker.Position.ToVec2(), 9f);
        var firstPlayer = MobileParty.FindNextLocatable(ref order);
        var secondPlayer = MobileParty.FindNextLocatable(ref order);
        AssignMapEvent(firstPlayer, firstEnemy);
        AssignMapEvent(secondPlayer, secondEnemy);

        NearbyPlayerSearch nearbyPlayers = default;
        Assert.Equal(0f, Score(attacker, firstEnemy, ref nearbyPlayers));
        Assert.Equal(0f, Score(attacker, secondEnemy, ref nearbyPlayers));
        Assert.Equal(UnsuppressedScore, Score(attacker, CreateParty(60f), ref nearbyPlayers));
        Assert.Equal(0f, Score(attacker, firstEnemy, ref nearbyPlayers));
    }

    private static void AssignMapEvent(MobileParty player, MobileParty enemy)
    {
        var battle = ObjectHelper.SkipConstructor<MapEvent>();
        player.Party._mapEventSide = new MapEventSide(battle, BattleSideEnum.Defender, player.Party);
        enemy.Party._mapEventSide = new MapEventSide(battle, BattleSideEnum.Attacker, enemy.Party);
    }

    private static float Score(MobileParty attacker, MobileParty enemy, ref NearbyPlayerSearch nearbyPlayers) =>
        DefaultMobilePartyAIModelPatches.SuppressAttackNearStrongerPlayer(attacker, enemy, UnsuppressedScore, ref nearbyPlayers);

    private MobileParty AddPlayer(float strength, float x, bool active = true)
    {
        var party = CreateParty(strength, x);
        party.IsActive = active;
        PlayerObjects.Add(party, new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));
        registeredPlayers.Add(party);
        Campaign.Current.MobilePartyLocator.UpdateLocator(party);
        return party;
    }

    private static MobileParty CreateParty(float strength, float x = 100f)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        party._locatorNodeIndex = -1;
        party._position = new CampaignVec2(new Vec2(x, 100f), isOnLand: true);
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        partyBase.MobileParty = party;
        partyBase.MemberRoster = ObjectHelper.SkipConstructor<TroopRoster>();
        partyBase._lastEstimatedStrengthVersionNo = 0;
        partyBase._cachedEstimatedStrength = strength;
        party.Party = partyBase;
        return party;
    }
}
